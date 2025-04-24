namespace TransactionService.Models
{
    public class Transaction
    {
        public Guid Id { get; init; }
        public required string TransferId { get; init; }
        public required int UserId { get; init; }
        public required string FromAccount { get; init; }
        public required string ToAccount { get; init; }
        public required decimal Amount { get; init; }
        public required string Status { get; set; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; set; }
    }
}
