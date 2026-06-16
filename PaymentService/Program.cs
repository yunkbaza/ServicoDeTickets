using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using MassTransit;
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

// 🔥 1. Conectando o PaymentService ao RabbitMQ para avisar o OrderService
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { h.Username("guest"); h.Password("guest"); });
        cfg.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(3)));
    });
});

var app = builder.Build();
app.UseCors("AllowAll");

// 🔥 2. Rota de Criação do Checkout (Atualizada para guardar o OrderId)
app.MapPost("/api/payment/create-session", async ([FromBody] CheckoutSessionRequest request, IConfiguration config) =>
{
    try
    {
        var stripeKey = config["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(stripeKey) || stripeKey.Contains("SUA_CHAVE"))
            return Results.BadRequest(new { message = "Chave da Stripe não configurada." });

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
            SuccessUrl = $"http://localhost:4200/checkout/success",
            CancelUrl = $"http://localhost:4200/checkout/cancel?eventId={request.EventId}&quantity={request.Quantity}",
            // 👇 Metadados Invisíveis: O Stripe vai guardar isso e nos devolver no Webhook!
            Metadata = new Dictionary<string, string>
            {
                { "EventId", request.EventId },
                { "Quantity", request.Quantity.ToString() },
                { "UserId", request.UserId ?? "anonymous" },
                { "OrderId", request.OrderId ?? Guid.NewGuid().ToString() } 
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        return Results.Ok(new { url = session.Url });
    }
    catch (Exception e)
    {
        return Results.BadRequest(new { message = e.Message });
    }
});

// 🔥 3. O Webhook: O Ouvido Absoluto do Sistema
app.MapPost("/api/payment/webhook", async (HttpRequest request, IConfiguration config, IPublishEndpoint publishEndpoint) =>
{
    var json = await new StreamReader(request.Body).ReadToEndAsync();
    var endpointSecret = config["Stripe:WebhookSecret"];

    try
    {
        // O Stripe assina a mensagem criptograficamente. Isso impede que hackers forjem pagamentos aprovados.
        var stripeEvent = EventUtility.ConstructEvent(json, request.Headers["Stripe-Signature"], endpointSecret);

        // 🔥 CORREÇÃO: Adicionado 'Stripe.' para garantir a leitura correta do SDK
        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
        {
            var session = stripeEvent.Data.Object as Session;

            var orderId = session.Metadata["OrderId"];
            var eventId = session.Metadata["EventId"];
            var quantity = int.Parse(session.Metadata["Quantity"]);
            var userId = session.Metadata["UserId"];

            // 🚀 SAGA APROVADO: Dispara a ordem final para gerar o bilhete!
            await publishEndpoint.Publish(new PaymentAcceptedEvent(orderId, eventId, quantity, userId));
            
            Console.WriteLine($"\n✅ [WEBHOOK RECEBIDO] O dinheiro caiu! Bilhete para a Order {orderId} gerado via RabbitMQ!\n");
        }

        return Results.Ok(); // Tem de responder 200 OK para o Stripe não tentar de novo
    }
    catch (StripeException e)
    {
        Console.WriteLine($"\n❌ [WEBHOOK ERRO DE SEGURANÇA]: {e.Message}");
        return Results.BadRequest();
    }
});

app.Run("http://localhost:5002");

// Contratos
public record CheckoutSessionRequest(string EventId, string EventName, decimal Price, int Quantity, string UserId, string OrderId);
public record PaymentAcceptedEvent(string OrderId, string EventId, int Quantity, string UserId);