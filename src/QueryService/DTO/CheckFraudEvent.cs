namespace QueryService.DTO;

public class CheckFraudEvent
{
    public string transferId { get; set; }
    public bool isFraud { get; set; }
    public string status { get; set; }
    public DateTime Timestamp { get; set; }
}