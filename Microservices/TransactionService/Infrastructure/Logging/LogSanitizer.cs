using System;
using System.Text.RegularExpressions;

namespace TransactionService.Infrastructure.Logging
{
    /// <summary>
    /// Utility class for sanitizing sensitive information in logs
    /// </summary>
    public static class LogSanitizer
    {
        /// <summary>
        /// Masks an account ID for secure logging
        /// </summary>
        public static string MaskAccountId(int accountId)
        {
            // Keep only the first digit and replace the rest with asterisks
            string accountString = accountId.ToString();
            if (accountString.Length <= 1)
                return accountString;

            return $"{accountString[0]}{'*' * (accountString.Length - 1)}";
        }

        /// <summary>
        /// Masks a monetary amount for secure logging
        /// </summary>
        public static string MaskAmount(decimal amount)
        {
            // Only show if it's small, medium or large amount
            if (amount < 10)
                return "small amount";
            else if (amount < 1000)
                return "medium amount";
            else
                return "large amount";
        }

        /// <summary>
        /// Masks a transfer ID for secure logging
        /// </summary>
        public static string MaskTransferId(string transferId)
        {
            if (string.IsNullOrEmpty(transferId))
                return "invalid-id";

            // Keep first 3 chars and last 4, replace rest with ...
            if (transferId.Length > 7)
                return $"{transferId.Substring(0, 3)}...{transferId.Substring(transferId.Length - 4)}";
            
            return "***"; // For very short IDs, just mask entirely
        }

        /// <summary>
        /// Sanitizes an error message by removing any potentially sensitive information
        /// </summary>
        public static string SanitizeErrorMessage(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return "[No error message]";

            // Remove any potential account numbers using regex
            errorMessage = Regex.Replace(errorMessage, @"\b\d{8,16}\b", "********");
            
            // Remove any potential credit card numbers 
            errorMessage = Regex.Replace(errorMessage, @"\b(?:\d{4}[- ]){3}\d{4}\b", "************");
            
            // Remove any potential email addresses
            errorMessage = Regex.Replace(errorMessage, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", "[EMAIL]");
            
            return errorMessage;
        }

        /// <summary>
        /// Sanitizes general log messages to remove sensitive information
        /// </summary>
        public static string SanitizeLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "[Empty message]";
                
            // Remove potential JSON data that might contain sensitive info
            message = Regex.Replace(message, @"\{[^{}]*""transferId""[^{}]*\}", "[TRANSACTION DATA]");
            
            // Remove potential account numbers
            message = Regex.Replace(message, @"""(accountId|fromAccount|toAccount)""\s*:\s*""?\d+""?", m => {
                string key = m.Value.Split(':')[0].Trim();
                return $"{key}: \"***\"";
            });
            
            // Remove potential amounts
            message = Regex.Replace(message, @"""amount""\s*:\s*\d+(\.\d+)?", "\"amount\": ***");
            
            return message;
        }
    }
}