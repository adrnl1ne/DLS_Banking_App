using System;

namespace TransactionService.API.Models;

public class TransactionResponse
{
    public string TransferId { get; set; } = string.Empty;
    public string FromAccount { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public static TransactionResponse FromTransaction(Transaction transaction)
    {
        return new TransactionResponse
        {
            TransferId = transaction.TransferId,
            FromAccount = transaction.FromAccount,
            ToAccount = transaction.ToAccount,
            Amount = transaction.Amount,
            Status = transaction.Status,
            CreatedAt = transaction.CreatedAt
        };
    }
}
