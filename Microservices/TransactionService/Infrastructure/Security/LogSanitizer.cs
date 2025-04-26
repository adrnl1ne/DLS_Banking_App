using System;
using System.Text.RegularExpressions;

namespace TransactionService.Infrastructure.Security
{
    /// <summary>
    /// Utility class for sanitizing sensitive information in logs
    /// </summary>
    public static class LogSanitizer
    {
        private static readonly Regex AccountNumberPattern = new(@"(?:account|Account)\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AmountPattern = new(@"\$?\d+\.\d{2}", RegexOptions.Compiled);
        private static readonly Regex JwtPattern = new(@"eyJ[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]*", RegexOptions.Compiled);

        /// <summary>
        /// Masks an account ID, showing only the last 2 digits
        /// </summary>
        public static string MaskAccountId(int id)
        {
            string idString = id.ToString();
            if (idString.Length <= 2)
                return "**" + idString;
            
            return new string('*', idString.Length - 2) + idString.Substring(idString.Length - 2);
        }

        /// <summary>
        /// Masks a transfer ID, showing only the last 4 characters
        /// </summary>
        public static string MaskTransferId(string transferId)
        {
            if (string.IsNullOrEmpty(transferId) || transferId.Length <= 4)
                return transferId;
                
            return "..." + transferId.Substring(transferId.Length - 4);
        }

        /// <summary>
        /// Masks a monetary amount
        /// </summary>
        public static string MaskAmount(decimal amount)
        {
            return "***.**";
        }
        
        /// <summary>
        /// Sanitizes a log message by masking sensitive data
        /// </summary>
        public static string SanitizeLogMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;
                
            // Mask account numbers
            message = AccountNumberPattern.Replace(message, m => 
                $"Account {new string('*', Math.Max(0, m.Groups[1].Value.Length - 2))}{m.Groups[1].Value.Substring(Math.Max(0, m.Groups[1].Value.Length - 2))}");
            
            // Mask dollar amounts
            message = AmountPattern.Replace(message, "***.**");
            
            // Mask JWT tokens
            message = JwtPattern.Replace(message, "[REDACTED JWT]");
            
            return message;
        }
        
        /// <summary>
        /// Sanitizes error messages, additionally removing stack traces
        /// </summary>
        public static string SanitizeErrorMessage(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return errorMessage;
                
            // Remove potential stack traces
            var stackTracePattern = new Regex(@"at\s+[\w\.\<\>]+\s+in\s+[\w\:\\\/\.]+:[0-9]+");
            errorMessage = stackTracePattern.Replace(errorMessage, "[stack trace removed]");
            
            // Sanitize the rest using the standard sanitizer
            return SanitizeLogMessage(errorMessage);
        }
    }
}