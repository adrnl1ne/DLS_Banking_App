namespace QueryService.DTO;

public class CheckFraudEvent
{
    public string TransferId { get; set; }
    public bool IsFraud { get; set; }
    public string Status { get; set; }
    public decimal Amount { get; set; }
    public string Timestamp { get; set; }
}