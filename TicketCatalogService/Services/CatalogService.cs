using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TicketCatalogService.Models;
using MassTransit; 
using TicketCatalogService.Events; 

namespace TicketCatalogService.Services;

public class CatalogService
{
    private readonly IMongoCollection<EventTicket> _eventsCollection;
    private readonly IPublishEndpoint _publishEndpoint; // Injetamos o publicador de mensagens

    public CatalogService(IConfiguration configuration, IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint; 
        
        // 1. Lê a string de conexão do appsettings.json
        var connectionString = configuration.GetSection("MongoDbSettings:ConnectionString").Value;
        var databaseName = configuration.GetSection("MongoDbSettings:DatabaseName").Value;

        // 2. Cria o cliente do Mongo e conecta no banco e na coleção
        var mongoClient = new MongoClient(connectionString);
        var mongoDatabase = mongoClient.GetDatabase(databaseName);

        _eventsCollection = mongoDatabase.GetCollection<EventTicket>("Events");
    }

    // Método para buscar todos os eventos
    public async Task<List<EventTicket>> GetAsync() =>
        await _eventsCollection.Find(_ => true).ToListAsync();

    // Método para criar um novo evento e disparar a mensagem
    public async Task CreateAsync(EventTicket newEvent)
    {
        // 1. Salva no MongoDB primeiro
        await _eventsCollection.InsertOneAsync(newEvent);

        // 2. Dispara a mensagem (evento) para o RabbitMQ
        await _publishEndpoint.Publish(new ShowCreatedEvent(
            newEvent.Id!, 
            newEvent.Name, 
            newEvent.EventDate, 
            newEvent.TotalTickets
        ));
    }
}