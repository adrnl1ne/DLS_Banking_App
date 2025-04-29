using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class AccountCreatedEvent
{   
    [JsonPropertyName("event_type")]
    public string EventType { get; set; }  // From "event_type"
    
    [JsonPropertyName("accountId")]
    public int AccountId { get; set; }
    
    [JsonPropertyName("userId")]
    public int UserId { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("amount")]
    public double Amount { get; set; }
    
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }
}
