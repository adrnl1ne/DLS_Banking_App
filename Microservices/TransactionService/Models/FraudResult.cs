namespace TransactionService.Models
{
    public class FraudResult
    {
        public string TransferId { get; set; } = string.Empty;
        public bool IsFraud { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}