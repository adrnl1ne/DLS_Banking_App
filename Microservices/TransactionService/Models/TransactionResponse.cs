using System;

namespace TransactionService.Models
{
    public class TransactionResponse
    {
        public string TransferId { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string FromAccount { get; set; } = string.Empty;
        public string ToAccount { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public static TransactionResponse FromTransaction(Transaction transaction)
        {
            return new TransactionResponse
            {
                TransferId = transaction.TransferId,
                UserId = transaction.UserId,
                FromAccount = transaction.FromAccount,
                ToAccount = transaction.ToAccount,
                Amount = transaction.Amount,
                Status = transaction.Status,
                CreatedAt = transaction.CreatedAt,
                UpdatedAt = transaction.UpdatedAt
            };
        }
    }
}
