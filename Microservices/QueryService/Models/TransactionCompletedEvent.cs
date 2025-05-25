using System.Text.Json.Serialization;

namespace QueryService.Models
{
    public class TransactionCompletedEvent
    {
        [JsonPropertyName("transferId")]
        public string TransferId { get; set; } = string.Empty;
        
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("fromAccount")]
        public string FromAccount { get; set; } = string.Empty;
        
        [JsonPropertyName("toAccount")]
        public string ToAccount { get; set; } = string.Empty;
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
        
        [JsonPropertyName("completedAt")]
        public DateTime CompletedAt { get; set; }
    }
}
