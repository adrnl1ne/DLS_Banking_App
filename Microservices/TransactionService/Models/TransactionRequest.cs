namespace TransactionService.Models
{
    public class TransactionRequest
    {
        public string FromAccount { get; init; } = string.Empty;
        public string ToAccount { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public int UserId { get; set; }
    }
}