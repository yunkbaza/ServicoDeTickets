using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TicketCatalogService.Models;

namespace TicketCatalogService.Services;

public class CatalogService
{
    private readonly IMongoCollection<EventTicket> _eventsCollection;

    public CatalogService(IConfiguration configuration)
    {
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

    // Método para criar um novo evento
    public async Task CreateAsync(EventTicket newEvent) =>
        await _eventsCollection.InsertOneAsync(newEvent);
}