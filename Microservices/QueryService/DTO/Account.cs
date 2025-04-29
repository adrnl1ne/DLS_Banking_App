using System.Text.Json.Serialization;

namespace QueryService.DTO;

public class Account
{   
    [JsonPropertyName("id")]
    public int id { get; set; }
    
    [JsonPropertyName("name")]
    public string name { get; set; }
    
    [JsonPropertyName("amount")]
    public double amount { get; set; }
    
    [JsonPropertyName("userId")]
    public int userId { get; set; }
}