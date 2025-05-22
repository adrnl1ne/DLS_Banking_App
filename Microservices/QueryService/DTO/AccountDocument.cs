using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class AccountDocument
{
    [JsonPropertyName("AccountId")]
    public int AccountId { get; set; }

    [JsonPropertyName("UserId")]
    public int UserId { get; set; }

    [JsonPropertyName("Name")]
    public required string Name { get; set; }

    [JsonPropertyName("Balance")]
    public decimal Balance { get; set; }

    [JsonPropertyName("LastUpdated")]
    public required string LastUpdated { get; set; }

    [JsonPropertyName("LastTransactionType")]
    public required string LastTransactionType { get; set; }
}