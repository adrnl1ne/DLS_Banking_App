using System;
using System.Text.Json.Serialization;

namespace TransactionService.Models
{
    public class AccountBalanceUpdateMessage
    {
        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }
        
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        
        [JsonPropertyName("transactionId")]
        public string TransactionId { get; set; } = string.Empty;
        
        [JsonPropertyName("transactionType")]
        public string TransactionType { get; set; } = string.Empty;
        
        [JsonPropertyName("isAdjustment")]
        public bool IsAdjustment { get; set; }
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}