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
// 2. INJEÇÃO DO MONGODB
// ==========================================
var mongoConn = "mongodb://localhost:27017";
var mongoDb = "BazaTicketReservationDb";
var mongoClient = new MongoClient(mongoConn);
var database = mongoClient.GetDatabase(mongoDb);

builder.Services.AddSingleton(database.GetCollection<TicketInventory>("Inventory"));

// ==========================================
// 3. MASSTRANSIT & RABBITMQ
// ==========================================
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ShowCreatedEventConsumer>();
    x.AddConsumer<PaymentRejectedEventConsumer>();

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

app.MapGet("/", () => "Ticket Reservation Service is Running!");

// ==========================================
// SAGA INITIATOR: Bloqueia o ingresso (Hold)
// ==========================================
app.MapPost("/api/reservations", async (
    ReservationRequest request,
    ClaimsPrincipal user,
    IMongoCollection<TicketInventory> inventory,
    IPublishEndpoint publishEndpoint,
    CancellationToken cancellationToken) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";

    var filter = Builders<TicketInventory>.Filter.And(
        Builders<TicketInventory>.Filter.Eq(x => x.EventId, request.EventId),
        Builders<TicketInventory>.Filter.Gte(x => x.AvailableTickets, request.Quantity)
    );

    var update = Builders<TicketInventory>.Update.Inc(x => x.AvailableTickets, -request.Quantity);

    var result = await inventory.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

    if (result.ModifiedCount == 0)
    {
        return Results.BadRequest(new { Message = "Ingressos esgotados ou quantidade indisponível no momento." });
    }

    var orderId = Guid.NewGuid().ToString();
    
    var reservedEvent = new TicketReservedEvent(orderId, request.EventId, request.Quantity, userId);
    await publishEndpoint.Publish(reservedEvent, cancellationToken);

    return Results.Ok(new { Message = "Reserva iniciada com sucesso!", OrderId = orderId });
}); // Removi o .RequireAuthorization() temporariamente para facilitar seu teste do fluxo SAGA

// ==========================================
// SAGA ROLLBACK: Devolve o ingresso pro lote
// ==========================================
app.MapDelete("/api/reservations/{eventId}/{quantity}", async (
    string eventId,
    int quantity,
    IMongoCollection<TicketInventory> inventory,
    CancellationToken cancellationToken) =>
{
    var filter = Builders<TicketInventory>.Filter.Eq(x => x.EventId, eventId);
    var update = Builders<TicketInventory>.Update.Inc(x => x.AvailableTickets, quantity);

    await inventory.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

    return Results.Ok(new { Message = "SAGA Rollback: Reserva cancelada e estoque devolvido." });
});

app.Run("http://localhost:5001");

public record ReservationRequest(string EventId, int Quantity);
public record InventoryUpdatedEvent(string EventId, int AvailableTickets);