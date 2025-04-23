namespace QueryService.DTO;

public class AccountCreatedEvent
{
    public string Event_Type { get; set; }
    public Guid AccountId { get; set; }
    public string UserId { get; set; }
    public string Name { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
}
