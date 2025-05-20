using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class AccountCreatedEvent
{
    [JsonPropertyName("event_type")]
    public required string EventType { get; set; }

    [JsonPropertyName("accountId")]
    public int AccountId { get; set; }

    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; set; }
}