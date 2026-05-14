using MassTransit;
using ReservationService.Consumers;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do MassTransit (RabbitMQ) como OUVINTE (Consumer)
builder.Services.AddMassTransit(x =>
{
    // Avisa o MassTransit que temos a classe consumidora
    x.AddConsumer<ShowCreatedEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });

        // 2. CRIA A FILA: O MassTransit vai criar essa fila no RabbitMQ e ligá-la à Exchange do evento
        cfg.ReceiveEndpoint("reservation-service-queue", e =>
        {
            e.ConfigureConsumer<ShowCreatedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

// Rota básica só para sabermos que a API está viva
app.MapGet("/", () => "🎫 Serviço de Reservas online e escutando o RabbitMQ!");

app.Run();