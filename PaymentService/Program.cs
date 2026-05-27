using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MassTransit;
using PaymentService.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🛡️ MASSTRANSIT COM RESILIÊNCIA ENTERPRISE (RETRY POLICY)
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TicketReservedEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });
        
        // 🔥 RESILIÊNCIA: Tenta conectar e processar a mensagem 5 vezes antes de jogar para a Dead Letter Queue (DLQ)
        cfg.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(5)));

        cfg.ReceiveEndpoint("payment-service-queue", e =>
        {
            // Opcional: Limite de concorrência para não sobrecarregar o banco/Stripe
            e.PrefetchCount = 16; 
            e.ConfigureConsumer<TicketReservedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => new 
{
    Service = "PaymentService",
    Status = "Running 💳",
    Resilience = "Ativada: Retry (5x) + DLQ",
    Message = "Aguardando reservas..."
});

app.Run("http://localhost:5176");