using System;

namespace TransactionService.Models
{
    public class TransactionLog
    {
        public string Id { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string LogType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty; 
        public string? SanitizedMessage { get; set; }
        public bool ContainsSensitiveData { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}