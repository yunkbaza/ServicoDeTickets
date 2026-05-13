using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TicketCatalogService.Models;

public class EventTicket
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("Name")]
    public string Name { get; set; } = null!;

    public DateTime EventDate { get; set; }

    public int TotalTickets { get; set; }
    
    public int AvailableTickets { get; set; }

    // Concorrência: Controle para saber se os ingressos já esgotaram
    public bool IsSoldOut => AvailableTickets <= 0; 
}