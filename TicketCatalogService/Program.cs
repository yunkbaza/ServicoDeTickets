using TicketCatalogService.Models;
using TicketCatalogService.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Injeção de Dependência do nosso serviço MongoDB
builder.Services.AddSingleton<CatalogService>();

// 2. Configuração da Documentação (Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 3. Força a interface visual do Swagger a aparecer sempre (sem bloqueios)
app.UseSwagger();
app.UseSwaggerUI();

// 4. Endpoints da nossa API de Ingressos

// Rota GET: Retorna todos os eventos do MongoDB
app.MapGet("/api/events", async (CatalogService catalogService) =>
{
    var events = await catalogService.GetAsync();
    return Results.Ok(events);
});

// Rota POST: Cria um novo evento no MongoDB
app.MapPost("/api/events", async (EventTicket newEvent, CatalogService catalogService) =>
{
    await catalogService.CreateAsync(newEvent);
    return Results.Created($"/api/events/{newEvent.Id}", newEvent);
});

app.Run();