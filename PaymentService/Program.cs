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

        // 3. Cria a Fila Exclusiva de Pagamentos
        cfg.ReceiveEndpoint("payment-service-queue", e =>
        {
            // 🔥 RESILIÊNCIA DE NÍVEL SÊNIOR (Retry Policy)
            // Se o processamento do cartão falhar (ex: instabilidade na Stripe),
            // o sistema não desiste na hora. Ele tenta mais 3 vezes, 
            // esperando 5 segundos entre cada tentativa.
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));

            // ⚠️ O Pulo do Gato (DLQ Automática):
            // Se falhar nas 3 tentativas, o MassTransit pega essa mensagem e joga 
            // automaticamente para a Fila de Mortos (payment-service-queue_error).
            // É lá que o nosso n8n via Docker vai ler para te avisar no Slack/WhatsApp!

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