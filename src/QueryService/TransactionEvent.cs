namespace QueryService;

public class TransactionEvent
{
    public string TransactionId { get; set; }
    public string Username { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public bool IsFraud { get; set; }
    public DateTime Timestamp { get; set; }
}
