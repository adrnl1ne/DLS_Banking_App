using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class TransactionEvent
{
    [JsonPropertyName("TransactionId")]
    public required string TransactionId { get; set; }

    [JsonPropertyName("Username")]
    public required string Username { get; set; }

    [JsonPropertyName("Amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("Status")]
    public required string Status { get; set; }
}
