using MassTransit;
using PaymentService.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Configuração do MassTransit (RabbitMQ)
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TicketReservedEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });

        // Cria a fila exclusiva de pagamentos
        cfg.ReceiveEndpoint("payment-service-queue", e =>
        {
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));

            e.ConfigureConsumer<TicketReservedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

app.MapGet("/", () => "💳 Serviço de Pagamentos blindado com Retry e DLQ!");

app.Run();