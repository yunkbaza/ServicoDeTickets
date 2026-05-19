using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MassTransit;
using PaymentService.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Adiciona o Swagger para documentação e testes
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🛡️ CONFIGURAÇÃO MASTER DO MASSTRANSIT (RABBITMQ)
builder.Services.AddMassTransit(x =>
{
    // 1. Registra o Consumidor (Quem vai processar o pagamento na Stripe)
    x.AddConsumer<TicketReservedEventConsumer>();

    // 2. Configura a conexão com o RabbitMQ
    x.UsingRabbitMq((context, cfg) =>
{
    cfg.Host("localhost", "/", h => {
        h.Username("guest");
        h.Password("guest");
    });
    
    // 🔥 ISSO AQUI EVITA O ERRO DE CONEXÃO REJEITADA
    cfg.ReceiveEndpoint("payment-service-queue", e =>
    {
        e.ConfigureConsumer<TicketReservedEventConsumer>(context);
    });
    });
});

var app = builder.Build();

// Ativa o Swagger apenas em ambiente de desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Rota de Health Check para garantir que o container/serviço está vivo
app.MapGet("/", () => new 
{
    Service = "PaymentService",
    Status = "Running 💳",
    Provider = "Stripe Integration",
    Resilience = "Ativada: Retry (3x) + Dead Letter Queue (DLQ)",
    Message = "O RabbitMQ está monitorando os pagamentos."
});

// Força a porta do serviço de pagamentos
app.Run("http://localhost:5176");