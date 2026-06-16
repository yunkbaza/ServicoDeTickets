using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("AllowAll");

app.MapPost("/api/payment/create-session", async ([FromBody] CheckoutSessionRequest request, IConfiguration config) =>
{
    try
    {
        // Lê a chave do appsettings.Development.json
        var stripeKey = config["Stripe:SecretKey"];

        if (string.IsNullOrWhiteSpace(stripeKey) || stripeKey.Contains("SUA_CHAVE"))
        {
            return Results.BadRequest(new { message = "Configure sua chave real do Stripe no appsettings.Development.json" });
        }

        StripeConfiguration.ApiKey = stripeKey;

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
                            Name = string.IsNullOrWhiteSpace(request.EventName) ? "Ingresso BazaTicket" : request.EventName,
                        },
                    },
                    Quantity = request.Quantity > 0 ? request.Quantity : 1,
                },
            },
            Mode = "payment",
            // URLs do Angular com o esquema de Rollback (SAGA)
            SuccessUrl = $"http://localhost:4200/checkout/success",
            CancelUrl = $"http://localhost:4200/checkout/cancel?eventId={request.EventId}&quantity={request.Quantity}",
            Metadata = new Dictionary<string, string>
            {
                { "EventId", request.EventId },
                { "Quantity", request.Quantity.ToString() },
                { "UserId", request.UserId ?? "anonymous" }
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return Results.Ok(new { url = session.Url });
    }
    catch (StripeException e)
    {
        return Results.BadRequest(new { message = e.StripeError.Message });
    }
    catch (Exception e)
    {
        return Results.BadRequest(new { message = e.Message });
    }
});

app.Run("http://localhost:5002");

public record CheckoutSessionRequest(string EventId, string EventName, decimal Price, int Quantity, string UserId);