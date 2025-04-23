namespace TransactionService.Models
{
    public class Transaction
    {
        public Guid Id { get; init; }
        public required string TransferId { get; init; }
        public int UserId { get; init; }
        public required string FromAccount { get; init; }
        public required string ToAccount { get; init; }
        public decimal Amount { get; init; }
        public required string Status { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; set; }
    }
}
