namespace ReservationService.Events
{
    // O Record agora carrega o UserId!
    public record TicketReservedEvent(string OrderId, string EventId, int Quantity, string UserId);
}