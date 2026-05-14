using TicketCatalogService.Models;
using TicketCatalogService.Services;
using MassTransit; 
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// Lê a conexão do Mongo para o HealthCheck saber onde testar
var mongoConn = builder.Configuration.GetSection("MongoDbSettings:ConnectionString").Value;

builder.Services.AddScoped<CatalogService>();

builder.Services.AddHealthChecks()
    .AddMongoDb(sp => new MongoClient(mongoConn), name: "MongoDB-CatalogDb");

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { h.Username("guest"); h.Password("guest"); });
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// 🔥 ROTA DE HEALTH CHECK
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