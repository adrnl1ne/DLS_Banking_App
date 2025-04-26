using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Security;
using TransactionService.Models;
using TransactionService.Infrastructure.Data;

namespace TransactionService.Infrastructure.Logging
{
    public interface ISecureTransactionLogger
    {
        Task LogTransactionEventAsync(string transactionId, string logType, string message);
    }

    public class SecureTransactionLogger : ISecureTransactionLogger
    {
        private readonly ILogger<SecureTransactionLogger> _logger;
        private readonly TransactionDbContext _dbContext;

        public SecureTransactionLogger(
            ILogger<SecureTransactionLogger> logger,
            TransactionDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task LogTransactionEventAsync(string transactionId, string logType, string message)
        {
            try
            {
                // Sanitize the message
                string sanitizedMessage = LogSanitizer.SanitizeLogMessage(message);
                bool containsSensitiveData = message != sanitizedMessage;
                
                // Create a new log entry
                var logEntry = new TransactionLog
                {
                    Id = Guid.NewGuid().ToString(),
                    TransactionId = transactionId,
                    LogType = logType,
                    Message = message, // Store original message in database
                    SanitizedMessage = sanitizedMessage,
                    ContainsSensitiveData = containsSensitiveData,
                    CreatedAt = DateTime.UtcNow
                };

                // Add to database
                await _dbContext.TransactionLogs.AddAsync(logEntry);
                await _dbContext.SaveChangesAsync();

                // Log to application logs with sanitized message only
                _logger.LogInformation(
                    "Transaction {TransactionId} log created: [{LogType}] {Message}", 
                    LogSanitizer.MaskTransferId(transactionId),
                    logType,
                    sanitizedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Error creating transaction log for {TransactionId}", 
                    LogSanitizer.MaskTransferId(transactionId));
            }
        }
    }
}