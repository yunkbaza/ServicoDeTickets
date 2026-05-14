using MassTransit;
using MongoDB.Driver;
using OrderService.Models;

// 1. O CONTRATO (Igual ao do PaymentService)
namespace PaymentService.Events
{
    public record PaymentAcceptedEvent(string OrderId);
}

// 2. O CONSUMIDOR
namespace OrderService.Consumers
{
    using PaymentService.Events;

    public class PaymentAcceptedEventConsumer : IConsumer<PaymentAcceptedEvent>
    {
        private readonly ILogger<PaymentAcceptedEventConsumer> _logger;
        private readonly IMongoCollection<TicketOrder> _orderCollection;

        public PaymentAcceptedEventConsumer(ILogger<PaymentAcceptedEventConsumer> logger, IConfiguration configuration)
        {
            _logger = logger;
            var mongoClient = new MongoClient(configuration["MongoDbSettings:ConnectionString"]);
            var database = mongoClient.GetDatabase(configuration["MongoDbSettings:DatabaseName"]);
            _orderCollection = database.GetCollection<TicketOrder>("Orders");
        }

        public async Task Consume(ConsumeContext<PaymentAcceptedEvent> context)
        {
            var payment = context.Message;
            
            _logger.LogInformation("🎉 Pagamento aprovado recebido! Emitindo ingresso digital para o Pedido {OrderId}", payment.OrderId);

            // Gera o ingresso oficial e salva no banco
            var finalOrder = new TicketOrder { OrderId = payment.OrderId };
            await _orderCollection.InsertOneAsync(finalOrder);

            _logger.LogInformation("🎟️ INGRESSO DIGITAL GERADO COM SUCESSO! (Salvo no OrderDb)");
        }
    }
}