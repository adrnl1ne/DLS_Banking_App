using System;

namespace TransactionService.API.Infrastructure.Messaging.Events;

public class TransactionStatusUpdatedEvent
{
    public required string TransferId { get; set; }
    public required string Status { get; set; }
    public bool IsFraud { get; set; }
    public DateTime UpdatedAt { get; set; }
}
