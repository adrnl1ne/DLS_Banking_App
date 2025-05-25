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
            // No longer showing any part of the transaction ID
            _logger.LogInformation(
                "SECURE LOG: Transaction event: {EventType}, Status: {Message}", 
                eventType, message);
                
            return Task.CompletedTask;
        }
    }
}