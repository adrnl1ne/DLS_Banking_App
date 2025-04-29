namespace TransactionService.Models
{
    public class FraudResult
    {
        public required string TransferId { get; set; }
        public bool IsFraud { get; set; }
        public required string Status { get; set; }
        public decimal Amount { get; set; }
        public required string Timestamp { get; set; }
    }
}