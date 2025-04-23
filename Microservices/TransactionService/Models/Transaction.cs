using System;

namespace TransactionService.Models;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransferId { get; set; } = string.Empty;
    public string FromAccount { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "pending"; // pending, approved, declined
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
