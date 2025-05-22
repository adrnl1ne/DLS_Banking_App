using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class DeletedAccount
{
    [JsonPropertyName("accountId")]
    public int AccountId { get; set; }

    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; }
}