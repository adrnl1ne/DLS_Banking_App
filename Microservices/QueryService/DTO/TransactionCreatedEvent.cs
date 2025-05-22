using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class TransactionCreatedEvent
{
    [JsonPropertyName("TransferId")]
    public required string TransferId { get; set; }

    [JsonPropertyName("Status")]
    public required string Status { get; set; }

    [JsonPropertyName("Amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("Description")]
    public required string Description { get; set; }

    [JsonPropertyName("FromAccount")]
    public required string FromAccount { get; set; }

    [JsonPropertyName("ToAccount")]
    public required string ToAccount { get; set; }

    [JsonPropertyName("CreatedAt")]
    public required string CreatedAt { get; set; }
}