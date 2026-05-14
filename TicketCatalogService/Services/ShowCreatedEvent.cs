namespace TicketCatalogService.Events;

// Usamos 'record' no .NET para mensagens porque elas são imutáveis (não mudam no meio do caminho)
public record ShowCreatedEvent(string EventId, string Name, DateTime EventDate, int TotalTickets);
