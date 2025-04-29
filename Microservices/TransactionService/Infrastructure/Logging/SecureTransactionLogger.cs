using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TransactionService.Infrastructure.Logging
{
    public class SecureTransactionLogger : ISecureTransactionLogger
    {
        private readonly ILogger<SecureTransactionLogger> _logger;

        public SecureTransactionLogger(ILogger<SecureTransactionLogger> logger)
        {
            _logger = logger;
        }

        public Task LogTransactionEventAsync(string transactionId, string eventType, string message)
        {
            // Simple implementation that masks the ID by showing only the last 4 characters
            string maskedId = transactionId.Length > 4 
                ? "..." + transactionId.Substring(transactionId.Length - 4) 
                : transactionId;
                
            _logger.LogInformation(
                "SECURE LOG: Transaction {TransactionId}, Event: {EventType}, Message: {Message}", 
                maskedId, eventType, message);
                
            return Task.CompletedTask;
        }
    }
}