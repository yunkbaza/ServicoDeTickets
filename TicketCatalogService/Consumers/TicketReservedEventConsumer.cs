using MassTransit;
using MongoDB.Driver;
using TicketCatalogService.Models;

// 1. O Contrato: Tem que ter o mesmo namespace de onde a mensagem nasceu (ReservationService)
namespace ReservationService.Events
{
    public record TicketReservedEvent(string OrderId, string EventId, int Quantity);
}

// 2. O Consumidor
namespace TicketCatalogService.Consumers
{
    using ReservationService.Events;

    public class TicketReservedEventConsumer : IConsumer<TicketReservedEvent>
    {
        private readonly IMongoCollection<EventTicket> _eventsCollection;
        private readonly ILogger<TicketReservedEventConsumer> _logger;

        public TicketReservedEventConsumer(IConfiguration configuration, ILogger<TicketReservedEventConsumer> logger)
        {
            _logger = logger;
            var mongoClient = new MongoClient(configuration["MongoDbSettings:ConnectionString"]);
            var database = mongoClient.GetDatabase(configuration["MongoDbSettings:DatabaseName"]);
            _eventsCollection = database.GetCollection<EventTicket>("Events");
        }

        public async Task Consume(ConsumeContext<TicketReservedEvent> context)
        {
            var reserva = context.Message;
            _logger.LogInformation("🔄 Sincronizando Vitrine (Catálogo): Subtraindo {Quantity} ingresso(s) do evento {EventId}", reserva.Quantity, reserva.EventId);

            // Filtra o evento pelo ID e subtrai a quantidade de forma atômica
            var filter = Builders<EventTicket>.Filter.Eq(x => x.Id, reserva.EventId);
            var update = Builders<EventTicket>.Update.Inc(x => x.AvailableTickets, -reserva.Quantity);

            await _eventsCollection.UpdateOneAsync(filter, update);
            
            _logger.LogInformation("✅ Vitrine atualizada com sucesso!");
        }
    }
}