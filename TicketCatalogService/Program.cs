using TicketCatalogService.Models;
using TicketCatalogService.Services;
using MassTransit; 
using MongoDB.Driver;
using TicketCatalogService.Consumers; // <-- Importando o consumidor

var builder = WebApplication.CreateBuilder(args);

var mongoConn = builder.Configuration.GetSection("MongoDbSettings:ConnectionString").Value;

builder.Services.AddScoped<CatalogService>();

builder.Services.AddHealthChecks()
    .AddMongoDb(sp => new MongoClient(mongoConn), name: "MongoDB-CatalogDb");

// 🔥 CONFIGURAÇÃO DO RABBITMQ NO CATÁLOGO
builder.Services.AddMassTransit(x =>
{
    // Registra o nosso consumidor que diminui o estoque
    x.AddConsumer<TicketReservedEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { h.Username("guest"); h.Password("guest"); });
        
        // Cria a fila do Catálogo para ouvir as reservas
        cfg.ReceiveEndpoint("catalog-service-queue", e =>
        {
            e.ConfigureConsumer<TicketReservedEventConsumer>(context);
        });
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapHealthChecks("/health");

app.MapGet("/api/events", async (CatalogService catalogService) =>
{
    var events = await catalogService.GetAsync();
    return Results.Ok(events);
});

app.MapPost("/api/events", async (EventTicket newEvent, CatalogService catalogService) =>
{
    await catalogService.CreateAsync(newEvent);
    return Results.Created($"/api/events/{newEvent.Id}", newEvent);
});

app.Run();