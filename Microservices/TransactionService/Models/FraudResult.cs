namespace TransactionService.Models
{
    public class FraudResult
    {
        public string TransferId { get; set; }
        public bool IsFraud { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Timestamp { get; set; }
    }
}