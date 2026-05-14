using MassTransit;

namespace ReservationService.Events
{
    public record TicketReservedEvent(string OrderId, string EventId, int Quantity);
}

namespace PaymentService.Events
{
    public record PaymentAcceptedEvent(string OrderId);
    
    // 1. ATUALIZAÇÃO: Adicionamos EventId e Quantity para o Rollback SAGA
    public record PaymentRejectedEvent(string OrderId, string EventId, int Quantity, string Reason);
}

namespace PaymentService.Consumers
{
    using ReservationService.Events;
    using PaymentService.Events;

    public class TicketReservedEventConsumer : IConsumer<TicketReservedEvent>
    {
        private readonly ILogger<TicketReservedEventConsumer> _logger;

        public TicketReservedEventConsumer(ILogger<TicketReservedEventConsumer> logger) => _logger = logger;

        public async Task Consume(ConsumeContext<TicketReservedEvent> context)
        {
            var reserva = context.Message;
            _logger.LogInformation("💳 Processando pagamento para o Pedido {OrderId}", reserva.OrderId);

            await Task.Delay(2000); // Simula o tempo do Gateway de Pagamento

            var isPaymentApproved = new Random().Next(1, 101) <= 80;

            if (isPaymentApproved)
            {
                _logger.LogInformation("✅ Pagamento APROVADO para {OrderId}!", reserva.OrderId);
                await context.Publish(new PaymentAcceptedEvent(reserva.OrderId));
            }
            else
            {
                _logger.LogWarning("❌ Pagamento RECUSADO para {OrderId}. Iniciando Rollback SAGA...", reserva.OrderId);
                // 2. ATUALIZAÇÃO: Enviamos os dados do evento de volta para a fila
                await context.Publish(new PaymentRejectedEvent(reserva.OrderId, reserva.EventId, reserva.Quantity, "Saldo Insuficiente"));
            }
        }
    }
}