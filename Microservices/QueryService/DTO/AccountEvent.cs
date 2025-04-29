namespace QueryService.DTO;

public class AccountEvent
{
    public string EventType { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; }
    public decimal Amount { get; set; }
    public string Timestamp { get; set; }

}