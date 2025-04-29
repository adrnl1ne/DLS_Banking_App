

namespace TransactionService.Infrastructure.Messaging.Events;

public class TransactionCreatedEvent
{
    public required string TransferId { get; set; }
    public required string FromAccount { get; set; }
    public required string ToAccount { get; set; }
    public decimal Amount { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
