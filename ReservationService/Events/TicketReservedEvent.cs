namespace ReservationService.Events;

// Esta é a mensagem que será atirada para o RabbitMQ assim que o stock for garantido
public record TicketReservedEvent(
    string OrderId, 
    string EventId, 
    int Quantity
);