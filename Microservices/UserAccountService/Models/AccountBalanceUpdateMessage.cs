using System;

namespace UserAccountService.Models
{
    // Make sure this class exactly matches the class used to serialize the message
    public class AccountBalanceUpdateMessage
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public bool IsAdjustment { get; set; }
        public DateTime Timestamp { get; set; }
        
        public override string ToString()
        {
            return $"AccountId={AccountId}, Amount={Amount}, TransactionId={TransactionId}, Type={TransactionType}";
        }
    }
}