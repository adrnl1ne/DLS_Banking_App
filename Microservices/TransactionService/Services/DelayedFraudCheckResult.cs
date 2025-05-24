using System;

namespace TransactionService.Services
{
    public class DelayedFraudCheckResult
    {
        public string EventType { get; set; } = string.Empty;
        public string TransferId { get; set; } = string.Empty;
        public bool IsFraud { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
