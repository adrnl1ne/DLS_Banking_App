using System;

namespace UserAccountService.Models
{
    public class AccountBalanceUpdateMessage
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public bool IsAdjustment { get; set; }
        public DateTime Timestamp { get; set; }
    }
}