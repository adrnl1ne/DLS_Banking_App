using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class CheckFraudEvent
{
    [JsonPropertyName("transferId")]
    public string? TransferId { get; set; }

    [JsonPropertyName("isFraud")]
    public bool? IsFraud { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}