    using MassTransit;
    using MongoDB.Driver;
    using ReservationService.Consumers;
    using ReservationService.Models;
    using ReservationService.Events;

    var builder = WebApplication.CreateBuilder(args);

    // 1. INJEÇÃO DO MONGODB: Para o podermos usar na nossa rota de vendas
    var mongoConn = builder.Configuration.GetSection("MongoDbSettings:ConnectionString").Value;
    var mongoDb = builder.Configuration.GetSection("MongoDbSettings:DatabaseName").Value;
    var mongoClient = new MongoClient(mongoConn);
    var database = mongoClient.GetDatabase(mongoDb);

    // Injetamos a "Tabela" de inventário para estar disponível em toda a app
    builder.Services.AddSingleton(database.GetCollection<TicketInventory>("Inventory"));

    // 2. CONFIGURAÇÃO DO MASSTRANSIT (RABBITMQ)
    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<ShowCreatedEventConsumer>();
        x.AddConsumer<PaymentRejectedEventConsumer>(); // <-- ADICIONAR ESTA LINHA

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host("localhost", "/", h => { h.Username("guest"); h.Password("guest"); });
            
            cfg.ReceiveEndpoint("reservation-service-queue", e =>
            {
                e.ConfigureConsumer<ShowCreatedEventConsumer>(context);
            });

            // <-- ADICIONAR ESTE NOVO ENDPOINT DE ROLLBACK
            cfg.ReceiveEndpoint("reservation-rollback-queue", e => 
            {
                e.ConfigureConsumer<PaymentRejectedEventConsumer>(context);
            });
        });
    });

    var app = builder.Build();

    app.MapGet("/", () => "🎫 Serviço de Reservas online e escutando o RabbitMQ!");

    // 🚀 A ROTA MESTRE: ALTA CONCORRÊNCIA E SAGA INITIATOR
    app.MapPost("/api/reservations", async (
        ReservationRequest request,
        IMongoCollection<TicketInventory> inventory,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken) =>
    {
        // --- DEBUG ---
        Console.WriteLine($"DEBUG: Recebido: EventId={request.EventId}, Qtd={request.Quantity}");
        var dbName = inventory.Database.DatabaseNamespace.DatabaseName;
        Console.WriteLine($"DEBUG: Conectado ao banco: {dbName}");
        // --------------

        var filter = Builders<TicketInventory>.Filter.And(
            Builders<TicketInventory>.Filter.Eq(x => x.EventId, request.EventId),
            Builders<TicketInventory>.Filter.Gte(x => x.AvailableTickets, request.Quantity)
        );

        var update = Builders<TicketInventory>.Update.Inc(x => x.AvailableTickets, -request.Quantity);

        var result = await inventory.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        // --- DEBUG ---
        Console.WriteLine($"DEBUG: Modificados: {result.ModifiedCount}");
        // --------------

        if (result.ModifiedCount == 0)
        {
            return Results.BadRequest(new { 
                Message = "🔥 Erro: Ingressos esgotados ou quantidade indisponível!",
                DebugInfo = $"Banco atual: {dbName}"
            });
        }

        var orderId = Guid.NewGuid().ToString();
        await publishEndpoint.Publish(new TicketReservedEvent(orderId, request.EventId, request.Quantity), cancellationToken);

        return Results.Ok(new { Message = "✅ Reservado!", OrderId = orderId });
    });

    app.Run();

    // DTO para receber os dados do Frontend
    public record ReservationRequest(string EventId, int Quantity);