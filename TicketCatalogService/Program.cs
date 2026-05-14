using TicketCatalogService.Models;
using TicketCatalogService.Services;
using MassTransit; // Necessário para o RabbitMQ

var builder = WebApplication.CreateBuilder(args);

// 1. Injeção de Dependência do nosso serviço MongoDB
builder.Services.AddScoped<CatalogService>();

// 2. Configuração do MassTransit (RabbitMQ)
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });
    });
});

// 3. Configuração da Documentação (Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 4. Força a interface visual do Swagger a aparecer sempre
app.UseSwagger();
app.UseSwaggerUI();

// 5. Endpoints da nossa API de Ingressos

// Rota GET: Retorna todos os eventos do MongoDB
app.MapGet("/api/events", async (CatalogService catalogService) =>
{
    var events = await catalogService.GetAsync();
    return Results.Ok(events);
});

// Rota POST: Cria um novo evento no MongoDB e dispara o evento no RabbitMQ
app.MapPost("/api/events", async (EventTicket newEvent, CatalogService catalogService) =>
{
    await catalogService.CreateAsync(newEvent);
    return Results.Created($"/api/events/{newEvent.Id}", newEvent);
});

app.Run();