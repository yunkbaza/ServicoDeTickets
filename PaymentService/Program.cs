using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using Stripe.Checkout;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowGateway", policy =>
    {
        policy.WithOrigins("http://localhost:5130", "http://localhost:4200")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowGateway");

StripeConfiguration.ApiKey = "sk_test_51P3K2vK3N5n7R8W9...sua_chave_secreta_aqui...";

app.MapPost("/api/payment/create-session", async (CheckoutSessionRequest request) =>
{
    var options = new SessionCreateOptions
    {
        PaymentMethodTypes = new List<string> { "card" },
        LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    UnitAmount = (long)(request.Price * 100),
                    Currency = "brl",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = request.EventName,
                    },
                },
                Quantity = request.Quantity,
            },
        },
        Mode = "payment",
        SuccessUrl = $"http://localhost:4200/checkout/success?eventId={request.EventId}&quantity={request.Quantity}",
        CancelUrl = "http://localhost:4200/checkout/cancel",
        Metadata = new Dictionary<string, string>
        {
            { "EventId", request.EventId },
            { "Quantity", request.Quantity.ToString() },
            { "UserId", request.UserId }
        }
    };

    var service = new SessionService();
    var session = await service.CreateAsync(options);

    return Results.Ok(new { url = session.Url });
});

app.Run("http://localhost:5002");

public record CheckoutSessionRequest(string EventId, string EventName, decimal Price, int Quantity, string UserId);