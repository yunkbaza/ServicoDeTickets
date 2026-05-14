using MassTransit;
using PaymentService.Consumers;

var builder = WebApplication.CreateBuilder(args);

// Configuração do MassTransit (RabbitMQ)
builder.Services.AddMassTransit(x =>
{
    // Adiciona o consumidor que acabamos de criar
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
            e.ConfigureConsumer<TicketReservedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

app.MapGet("/", () => "💳 Serviço de Pagamentos online e escutando a fila de SAGA!");

app.Run();