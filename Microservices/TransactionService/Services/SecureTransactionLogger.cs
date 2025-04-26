using System;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Data;

namespace TransactionService.Services
{
    public class SecureTransactionLogger : ITransactionLogger
    {
        private readonly ILogger<SecureTransactionLogger> _logger;
        private readonly ITransactionRepository _repository;
        
        public SecureTransactionLogger(ILogger<SecureTransactionLogger> logger, ITransactionRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }
        
        public async Task LogTransactionEventAsync(string transactionId, string logType, string message)
        {
            // Sanitize the message before logging
            string sanitizedMessage = SanitizeSensitiveData(message);
            
            try
            {
                // Log to database with sanitized message
                await _repository.AddTransactionLogAsync(transactionId, logType, sanitizedMessage);
                
                // Only log minimal info to application logs
                _logger.LogInformation("Transaction event logged: {TransactionIdSuffix}, Type: {LogType}", 
                    GetIdSuffix(transactionId), logType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log transaction event for {TransactionIdSuffix}", 
                    GetIdSuffix(transactionId));
            }
        }
        
        private string SanitizeSensitiveData(string message)
        {
            // Replace account numbers with masked versions
            message = Regex.Replace(message, @"Account\s+(\d{4,})", m => 
                $"Account ****{m.Groups[1].Value.Substring(Math.Max(0, m.Groups[1].Value.Length - 2))}");
            
            // Replace specific amounts
            message = Regex.Replace(message, @"(\$?\d+\.\d{2})", "***.**");
            
            // Replace any JWT tokens
            message = Regex.Replace(message, @"eyJ[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*", "[REDACTED]");
            
            return message;
        }
        
        private string GetIdSuffix(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length <= 4)
                return id;
                
            return "..." + id.Substring(id.Length - 4);
        }
    }

    public interface ITransactionLogger
    {
        Task LogTransactionEventAsync(string transactionId, string logType, string message);
    }
}