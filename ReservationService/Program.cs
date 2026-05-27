using System.Security.Claims;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using ReservationService.Consumers;
using ReservationService.Events;
using ReservationService.Models;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. CONFIGURAÇÃO DE SEGURANÇA (JWT BEARER)
// ==========================================
// Esta é a chave exata que o IdentityService usa para assinar o Token.
var secretKey = "BazaTicketSuperSecretKeyForJwtAuthentication2026"; 

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "BazaTicketIdentity",
            ValidateAudience = true,
            ValidAudience = "BazaTicketFrontend",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==========================================
// 2. INJEÇÃO DO MONGODB
// ==========================================
var mongoConn = builder.Configuration.GetSection("MongoDbSettings:ConnectionString").Value ?? "mongodb://localhost:27017";
var mongoDb = builder.Configuration.GetSection("MongoDbSettings:DatabaseName").Value ?? "BazaTicketReservationDb";
var mongoClient = new MongoClient(mongoConn);
var database = mongoClient.GetDatabase(mongoDb);

builder.Services.AddSingleton(database.GetCollection<TicketInventory>("Inventory"));

// ==========================================
// 3. CONFIGURAÇÃO DO MASSTRANSIT (RABBITMQ)
// ==========================================
builder.Services.AddMassTransit(x =>
{
    // Registra os consumidores que escutam eventos de outros serviços
    x.AddConsumer<ShowCreatedEventConsumer>();
    x.AddConsumer<PaymentRejectedEventConsumer>(); 

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { 
            h.Username("guest"); 
            h.Password("guest"); 
        });
        
        // Resiliência: Tenta processar 3 vezes antes de falhar
        cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(3)));

        // Fila 1: Ouve quando um show é criado no Catálogo para clonar o estoque
        cfg.ReceiveEndpoint("reservation-service-queue", e =>
        {
            e.ConfigureConsumer<ShowCreatedEventConsumer>(context);
        });

        // Fila 2: Ouve quando um pagamento falha, para DEVOLVER o ingresso ao estoque (Compensação SAGA)
        cfg.ReceiveEndpoint("reservation-rollback-queue", e => 
        {
            e.ConfigureConsumer<PaymentRejectedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

// Ativa os middlewares de segurança e Swagger
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "🎫 Serviço de Reservas online, seguro e escutando o RabbitMQ!");

// ==========================================
// 🚀 A ROTA MESTRE: LOCK ATÔMICO E SAGA INITIATOR
// ==========================================
// O .RequireAuthorization() garante que só chegue aqui quem tem JWT válido
app.MapPost("/api/reservations", async (
    ReservationRequest request,
    ClaimsPrincipal user, // O .NET injeta automaticamente os dados do usuário do JWT aqui
    IMongoCollection<TicketInventory> inventory,
    IPublishEndpoint publishEndpoint,
    CancellationToken cancellationToken) =>
{
    // 1. Extração da Identidade (QUEM ESTÁ COMPRANDO?)
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var userEmail = user.FindFirst(ClaimTypes.Email)?.Value;

    if (string.IsNullOrEmpty(userId))
    {
        return Results.Unauthorized();
    }

    Console.WriteLine($"\n[RESERVA INICIADA] Usuário: {userEmail} | Evento: {request.EventId} | Qtd: {request.Quantity}");

    // 2. Lock Atômico no MongoDB (PREVENÇÃO DE OVERBOOKING)
    var filter = Builders<TicketInventory>.Filter.And(
        Builders<TicketInventory>.Filter.Eq(x => x.EventId, request.EventId),
        Builders<TicketInventory>.Filter.Gte(x => x.AvailableTickets, request.Quantity)
    );

    var update = Builders<TicketInventory>.Update.Inc(x => x.AvailableTickets, -request.Quantity);

    var result = await inventory.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

    if (result.ModifiedCount == 0)
    {
        Console.WriteLine("❌ [FALHA] Estoque insuficiente.");
        return Results.BadRequest(new { Message = "Ingressos esgotados ou quantidade indisponível no momento." });
    }

    // 3. SAGA Step 1: Disparo do Evento para a Fila (RabbitMQ)
    var orderId = Guid.NewGuid().ToString();
    
    // NOTA: Passamos o userId no evento para que o OrderService saiba de quem é o ingresso!
    var reservedEvent = new TicketReservedEvent(orderId, request.EventId, request.Quantity, userId);
    await publishEndpoint.Publish(reservedEvent, cancellationToken);

    Console.WriteLine($"✅ [SUCESSO] Lock efetuado. OrderId: {orderId}. Aguardando PaymentService...");

    return Results.Ok(new { Message = "Reserva iniciada com sucesso!", OrderId = orderId });
}).RequireAuthorization(); // 👈 FUNDAMENTAL PARA A SEGURANÇA DO ENDPOINT

app.Run();

// ==========================================
// Mapeamento de DTOs da API
// ==========================================
public record ReservationRequest(string EventId, int Quantity);