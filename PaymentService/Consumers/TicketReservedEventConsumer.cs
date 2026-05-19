using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MassTransit;
using Stripe;

// 1. O CONTRATO (Clonamos a assinatura do evento que vem da Reserva para o RabbitMQ conseguir ler)
namespace ReservationService.Events
{
    public record TicketReservedEvent(string OrderId, string EventId, int Quantity);
}

namespace PaymentService.Consumers
{
    using ReservationService.Events;

    // 2. OS EVENTOS DE SAÍDA DO SAGA
    public record PaymentAcceptedEvent(string OrderId, string EventId, int Quantity);
    public record PaymentRejectedEvent(string OrderId, string EventId, int Quantity, string Reason);

    public class TicketReservedEventConsumer : IConsumer<TicketReservedEvent>
    {
        private readonly ILogger<TicketReservedEventConsumer> _logger;

        // Injetamos o IConfiguration para ler o appsettings.json!
        public TicketReservedEventConsumer(ILogger<TicketReservedEventConsumer> logger, IConfiguration configuration)
        {
            _logger = logger;

            // Agora a chave vem do cofre, e não está mais exposta no código
            StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"]; 
        }

        public async Task Consume(ConsumeContext<TicketReservedEvent> context)
        {
            var reserva = context.Message;
            _logger.LogInformation("💳 Processando pagamento na Stripe para o Pedido: {OrderId}", reserva.OrderId);

            try
            {
                // Prepara a cobrança na Stripe
                var options = new PaymentIntentCreateOptions
                {
                    // O valor na Stripe é em centavos (R$ 150,00 * Quantidade)
                    Amount = reserva.Quantity * 15000, 
                    Currency = "brl",
                    PaymentMethod = "pm_card_visa", // ID de um cartão de teste VISA genérico da Stripe
                    Confirm = true, // Tenta cobrar imediatamente
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true,
                        AllowRedirects = "never" // Isso aqui já diz para a Stripe não pedir ReturnUrl!
                    }
                };

                var service = new PaymentIntentService();
                
                // Faz a chamada REAL para os servidores da Stripe nos EUA
                PaymentIntent intent = await service.CreateAsync(options);

                if (intent.Status == "succeeded")
                {
                    _logger.LogInformation("✅ Stripe aprovou o pagamento! ID Transação: {StripeId}", intent.Id);
                    
                    // Publica no RabbitMQ que deu tudo certo -> O OrderService vai ouvir!
                    await context.Publish(new PaymentAcceptedEvent(reserva.OrderId, reserva.EventId, reserva.Quantity));
                }
                else
                {
                    _logger.LogWarning("❌ Pagamento recusado pela Stripe. Status: {Status}", intent.Status);
                    
                    // Publica no RabbitMQ que falhou -> O ReservationService faz o Rollback!
                    await context.Publish(new PaymentRejectedEvent(reserva.OrderId, reserva.EventId, reserva.Quantity, "Recusado pela operadora do cartão"));
                }
            }
            catch (StripeException e)
            {
                _logger.LogError("🔥 Erro na API da Stripe: {Mensagem}", e.StripeError.Message);
                
                // Rollback pelo SAGA caso dê exceção na Stripe (ex: cartão sem limite)
                await context.Publish(new PaymentRejectedEvent(reserva.OrderId, reserva.EventId, reserva.Quantity, e.StripeError.Message));
            }
        }
    }
}