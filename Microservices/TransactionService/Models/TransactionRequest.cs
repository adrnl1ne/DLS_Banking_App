namespace TransactionService.Models
{
    public class TransactionRequest
    {
        public string FromAccount { get; set; } = string.Empty;
        public string ToAccount { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int UserId { get; set; }
    }
}