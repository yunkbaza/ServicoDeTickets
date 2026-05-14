using MassTransit;
using MongoDB.Driver;
using OrderService.Consumers;
using OrderService.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuração do MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentAcceptedEventConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => { h.Username("guest"); h.Password("guest"); });
        
        cfg.ReceiveEndpoint("order-service-queue", e =>
        {
            e.ConfigureConsumer<PaymentAcceptedEventConsumer>(context);
        });
    });
});

// 2. Injeção do MongoDB para usarmos na rota GET
builder.Services.AddSingleton(sp => 
{
    var config = sp.GetRequiredService<IConfiguration>();
    var client = new MongoClient(config["MongoDbSettings:ConnectionString"]);
    return client.GetDatabase(config["MongoDbSettings:DatabaseName"]).GetCollection<TicketOrder>("Orders");
});

var app = builder.Build();

app.MapGet("/", () => "📦 Serviço de Pedidos online. Aguardando pagamentos...");

// Rota para listar os ingressos emitidos
app.MapGet("/api/orders", async (IMongoCollection<TicketOrder> collection) => 
{
    return await collection.Find(_ => true).ToListAsync();
});

app.Run();