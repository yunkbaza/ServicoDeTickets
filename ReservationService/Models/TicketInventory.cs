using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ReservationService.Models;

public class TicketInventory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string EventId { get; set; } = null!; 

    public string Name { get; set; } = null!;
    
    public int AvailableTickets { get; set; }
}