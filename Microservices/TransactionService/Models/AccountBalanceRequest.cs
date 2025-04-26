using System.Text.Json.Serialization;

namespace TransactionService.Models
{
    public class AccountBalanceRequest
    {
        [JsonPropertyName("transactionType")]
        public string TransactionType { get; set; } = string.Empty;
        
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        
        [JsonPropertyName("isAdjustment")]
        public bool IsAdjustment { get; set; } = true;
        
        [JsonPropertyName("transactionId")]
        public string TransactionId { get; set; } = string.Empty;
    }
}