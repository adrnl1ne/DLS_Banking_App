using System;
using System.Text.Json.Serialization;

namespace TransactionService.Models
{
    public class AccountBalanceUpdate
    {
        [JsonPropertyName("accountId")]
        public int AccountId { get; set; }
        
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }
        
        [JsonPropertyName("transactionId")]
        public string TransactionId { get; set; } = string.Empty;
        
        [JsonPropertyName("isWithdrawal")]
        public bool IsWithdrawal { get; set; }
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}