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
// 1. CONFIGURAÇÃO DE SEGURANÇA (JWT)
// ==========================================
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
// 2. INJEÇÃO DO MONGODB (PREPARADO PARA OUTBOX)
// ==========================================
var mongoConn = "mongodb://localhost:27017";
var mongoDb = "BazaTicketReservationDb";
var mongoClient = new MongoClient(mongoConn);
var database = mongoClient.GetDatabase(mongoDb);

// Injetamos o Client e a Database globais para o MassTransit conseguir ler o Outbox e iniciar Transações
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton<IMongoDatabase>(database);
builder.Services.AddSingleton(database.GetCollection<TicketInventory>("Inventory"));

// ==========================================
// 3. MASSTRANSIT & RABBITMQ (COM TRANSACTIONAL OUTBOX)
// ==========================================
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ShowCreatedEventConsumer>();
    x.AddConsumer<PaymentRejectedEventConsumer>();

    // 🛡️ A MÁGICA: As mensagens vão para uma "caixa de saída" no MongoDB na mesma transação da reserva
    x.AddMongoDbOutbox(o =>
    {
        o.ClientFactory(provider => provider.GetRequiredService<IMongoClient>());
        o.DatabaseFactory(provider => provider.GetRequiredService<IMongoDatabase>());
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(3)));

        cfg.ReceiveEndpoint("reservation-service-queue", e =>
        {
            e.ConfigureConsumer<ShowCreatedEventConsumer>(context);
        });

        cfg.ReceiveEndpoint("reservation-rollback-queue", e => 
        {
            e.ConfigureConsumer<PaymentRejectedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "Ticket Reservation Service is Running with Outbox Pattern!");

// ==========================================
// SAGA INITIATOR: Bloqueia o ingresso com TRANSAÇÃO ACID
// ==========================================
app.MapPost("/api/reservations", async (
    ReservationRequest request,
    ClaimsPrincipal user,
    IMongoClient client, // Injetamos o client para controlar a sessão da transação
    IMongoCollection<TicketInventory> inventory,
    IPublishEndpoint publishEndpoint,
    CancellationToken cancellationToken) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

    // 🔥 INICIA A TRANSAÇÃO (Tudo ou Nada)
    using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
    session.StartTransaction();

    try
    {
        var filter = Builders<TicketInventory>.Filter.And(
            Builders<TicketInventory>.Filter.Eq(x => x.EventId, request.EventId),
            Builders<TicketInventory>.Filter.Gte(x => x.AvailableTickets, request.Quantity)
        );

        var update = Builders<TicketInventory>.Update.Inc(x => x.AvailableTickets, -request.Quantity);
        
        // ReturnDocument.After garante que a variável 'result' tem a quantidade atualizada de stock
        var options = new FindOneAndUpdateOptions<TicketInventory> { ReturnDocument = ReturnDocument.After };

        // Passamos a 'session' no primeiro parâmetro para garantir que faz parte da transação
        var result = await inventory.FindOneAndUpdateAsync(session, filter, update, options, cancellationToken);

        if (result == null)
        {
            await session.AbortTransactionAsync(cancellationToken);
            return Results.BadRequest(new { Message = "Ingressos esgotados ou quantidade indisponível no momento." });
        }

        var orderId = Guid.NewGuid().ToString();

        // 🛡️ O Outbox garante que estas mensagens ficam presas na transação até o commit
        await publishEndpoint.Publish(new TicketReservedEvent(orderId, request.EventId, request.Quantity, userId), cancellationToken);
        
        // 🔥 AVISA O SIGNALR DO NOVO STOCK
        await publishEndpoint.Publish(new InventoryUpdatedEvent(request.EventId, result.AvailableTickets), cancellationToken);

        // O MOMENTO DA VERDADE: Guarda o inventário E as mensagens do MassTransit na base de dados de uma vez só!
        await session.CommitTransactionAsync(cancellationToken);

        return Results.Ok(new { Message = "Reserva iniciada com sucesso e garantida pelo Outbox!", OrderId = orderId });
    }
    catch (Exception)
    {
        await session.AbortTransactionAsync(cancellationToken);
        return Results.BadRequest(new { Message = "Falha crítica na infraestrutura ao processar a reserva." });
    }
}); 

// ==========================================
// SAGA ROLLBACK: Devolve o ingresso pro lote (Também com Transação)
// ==========================================
app.MapDelete("/api/reservations/{eventId}/{quantity}", async (
    string eventId,
    int quantity,
    IMongoClient client,
    IMongoCollection<TicketInventory> inventory,
    IPublishEndpoint publishEndpoint,
    CancellationToken cancellationToken) =>
{
    using var session = await client.StartSessionAsync(cancellationToken: cancellationToken);
    session.StartTransaction();

    try 
    {
        var filter = Builders<TicketInventory>.Filter.Eq(x => x.EventId, eventId);
        var update = Builders<TicketInventory>.Update.Inc(x => x.AvailableTickets, quantity);
        var options = new FindOneAndUpdateOptions<TicketInventory> { ReturnDocument = ReturnDocument.After };

        var result = await inventory.FindOneAndUpdateAsync(session, filter, update, options, cancellationToken);

        if (result != null)
        {
            // 🔥 AVISA O SIGNALR QUE O BILHETE VOLTOU AO MERCADO!
            await publishEndpoint.Publish(new InventoryUpdatedEvent(eventId, result.AvailableTickets), cancellationToken);
        }

        await session.CommitTransactionAsync(cancellationToken);
        return Results.Ok(new { Message = "SAGA Rollback: Reserva cancelada e estoque devolvido com segurança Outbox." });
    }
    catch (Exception)
    {
        await session.AbortTransactionAsync(cancellationToken);
        return Results.BadRequest(new { Message = "Falha no processo de Rollback." });
    }
});

app.Run("http://localhost:5001");

// Contratos DTO e Mensagens
public record ReservationRequest(string EventId, int Quantity);
public record InventoryUpdatedEvent(string EventId, int AvailableTickets);