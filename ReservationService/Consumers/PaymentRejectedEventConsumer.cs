using MassTransit;
using MongoDB.Driver;
using ReservationService.Models;

// Contrato idêntico ao do PaymentService
namespace PaymentService.Events
{
    public record PaymentRejectedEvent(string OrderId, string EventId, int Quantity, string Reason);
}

namespace ReservationService.Consumers
{
    using PaymentService.Events;

    public class PaymentRejectedEventConsumer : IConsumer<PaymentRejectedEvent>
    {
        private readonly ILogger<PaymentRejectedEventConsumer> _logger;
        private readonly IMongoCollection<TicketInventory> _inventoryCollection;

        public PaymentRejectedEventConsumer(ILogger<PaymentRejectedEventConsumer> logger, IMongoCollection<TicketInventory> inventory)
        {
            _logger = logger;
            _inventoryCollection = inventory;
        }

        public async Task Consume(ConsumeContext<PaymentRejectedEvent> context)
        {
            var falha = context.Message;
            _logger.LogWarning("⚠️ SAGA Rollback: Pagamento falhou (Pedido {OrderId}). Devolvendo {Quantity} ingresso(s) ao estoque...", falha.OrderId, falha.Quantity);

            // Operação Atômica Reversa: Adiciona (+ falha.Quantity) de volta ao estoque
            var filter = Builders<TicketInventory>.Filter.Eq(x => x.EventId, falha.EventId);
            var update = Builders<TicketInventory>.Update.Inc(x => x.AvailableTickets, falha.Quantity);

            await _inventoryCollection.UpdateOneAsync(filter, update);

            _logger.LogInformation("🔄 Rollback concluído com sucesso! Ingressos disponíveis novamente para venda.");
        }
    }
}