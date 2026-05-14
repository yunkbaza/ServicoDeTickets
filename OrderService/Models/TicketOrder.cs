using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace OrderService.Models;

public class TicketOrder
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    public string OrderId { get; set; } = null!;
    
    public string Status { get; set; } = "Ingresso Emitido e Garantido";
    
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}