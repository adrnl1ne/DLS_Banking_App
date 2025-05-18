using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class TransactionCreatedEvent
{
    [JsonPropertyName("TransferId")]
    public string TransferId { get; set; }

    [JsonPropertyName("Status")]
    public string Status { get; set; }

    [JsonPropertyName("Amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("Description")]
    public string Description { get; set; }

    [JsonPropertyName("FromAccount")]
    public string FromAccount { get; set; } // Changed from int to string

    [JsonPropertyName("ToAccount")]
    public string ToAccount { get; set; }   // Changed from int to string

    [JsonPropertyName("CreatedAt")]
    public string CreatedAt { get; set; }
}