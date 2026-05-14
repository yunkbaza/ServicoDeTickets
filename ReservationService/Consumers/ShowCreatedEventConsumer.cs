using MassTransit;
using MongoDB.Driver;
using ReservationService.Models;

// 1. O CONTRATO DA MENSAGEM: O namespace precisa ser EXATAMENTE IGUAL ao do publicador
namespace TicketCatalogService.Events
{
    public record ShowCreatedEvent(string EventId, string Name, DateTime EventDate, int TotalTickets);
}

// 2. O SEU CONSUMIDOR: Fica no namespace normal do Serviço de Reservas
namespace ReservationService.Consumers
{
    using TicketCatalogService.Events; // Importa a mensagem ali de cima

    public class ShowCreatedEventConsumer : IConsumer<ShowCreatedEvent>
    {
        private readonly ILogger<ShowCreatedEventConsumer> _logger;
        private readonly IMongoCollection<TicketInventory> _inventoryCollection;

        public ShowCreatedEventConsumer(ILogger<ShowCreatedEventConsumer> logger, IConfiguration configuration)
        {
            _logger = logger;

            var connectionString = configuration.GetSection("MongoDbSettings:ConnectionString").Value;
            var databaseName = configuration.GetSection("MongoDbSettings:DatabaseName").Value;

            var mongoClient = new MongoClient(connectionString);
            var mongoDatabase = mongoClient.GetDatabase(databaseName);
            _inventoryCollection = mongoDatabase.GetCollection<TicketInventory>("Inventory");
        }

        public async Task Consume(ConsumeContext<ShowCreatedEvent> context)
        {
            var evento = context.Message;
            
            _logger.LogInformation("🎫 Recebido evento do RabbitMQ: Criando estoque para '{Name}'", evento.Name);

            var inventory = new TicketInventory
            {
                EventId = evento.EventId,
                Name = evento.Name,
                AvailableTickets = evento.TotalTickets
            };

            await _inventoryCollection.InsertOneAsync(inventory);

            _logger.LogInformation("✅ Estoque de {Tickets} ingressos liberado para vendas no banco local!", evento.TotalTickets);
        }
    }
}