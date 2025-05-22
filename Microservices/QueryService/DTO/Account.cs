using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class Account
{
    [JsonPropertyName("accountId")]
    public int accountId { get; set; }

    [JsonPropertyName("userId")]
    public int userId { get; set; }

    [JsonPropertyName("name")]
    public required string name { get; set; }

    [JsonPropertyName("balance")]
    public decimal balance { get; set; }
}