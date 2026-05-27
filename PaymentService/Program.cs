using MassTransit;
using PaymentService.Consumers;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<TicketReservedEventConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { h.Username("guest"); h.Password("guest"); });
        cfg.ReceiveEndpoint("payment-service-queue", e =>
        {
            e.ConfigureConsumer<TicketReservedEventConsumer>(context);
        });
    });
});

var app = builder.Build();

app.MapPost("/api/payment/create-intent", async (PaymentRequest request) =>
{
    var options = new PaymentIntentCreateOptions
    {
        Amount = (long)(request.Amount * 100),
        Currency = "brl",
        AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
    };

    var service = new PaymentIntentService();
    var intent = await service.CreateAsync(options);

    return Results.Ok(new { clientSecret = intent.ClientSecret });
});

app.Run();

public record PaymentRequest(decimal Amount);