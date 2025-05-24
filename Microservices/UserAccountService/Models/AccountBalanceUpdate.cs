using System;

namespace UserAccountService.Models
{
    public class AccountBalanceUpdate
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public bool IsWithdrawal { get; set; }
        public DateTime Timestamp { get; set; }
    }
}