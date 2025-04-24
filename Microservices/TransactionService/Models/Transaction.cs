using System;

namespace TransactionService.Models;

public class Transaction
{
    public string Id { get; set; } = string.Empty;
    public string TransferId { get; set; } = string.Empty;
    public string FromAccount { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = string.Empty;
    public string? TransactionType { get; set; }
    public string? Description { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } // Make nullable to handle NULL values in DB
}
