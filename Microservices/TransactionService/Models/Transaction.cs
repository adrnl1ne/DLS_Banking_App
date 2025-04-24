using System;

namespace TransactionService.Models
{
    public class Transaction
    {
        public Guid Id { get; set; }
        public required string TransferId { get; set; }
        public int UserId { get; set; }
        public required string FromAccount { get; set; }
        public required string ToAccount { get; set; }
        public decimal Amount { get; set; }
        public required string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
