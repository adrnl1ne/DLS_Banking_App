using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Security;
using TransactionService.Models;

namespace TransactionService.Infrastructure.Data.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly TransactionDbContext _context;
        private readonly ILogger<TransactionRepository> _logger;
        private readonly Infrastructure.Logging.ISecureTransactionLogger _secureLogger;

        public TransactionRepository(
            TransactionDbContext context, 
            ILogger<TransactionRepository> logger,
            Infrastructure.Logging.ISecureTransactionLogger secureLogger)
        {
            _context = context;
            _logger = logger;
            _secureLogger = secureLogger;
        }

        public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
        {
            // Secure logging - mask sensitive details
            _logger.LogInformation("Creating new transaction with ID: {TransactionId}", 
                LogSanitizer.MaskTransferId(transaction.TransferId));

            await _context.Transactions.AddAsync(transaction);
            await _context.SaveChangesAsync();

            // Use secure logger for full transaction details
            await _secureLogger.LogTransactionEventAsync(
                transaction.Id,
                "status_change",
                $"Transaction created with status: {transaction.Status} - From account {transaction.FromAccount} to account {transaction.ToAccount} for {transaction.Amount}");

            return transaction;
        }

        public async Task<Transaction?> GetTransactionByIdAsync(string id)
        {
            // Sanitize ID for logging
            _logger.LogInformation("Getting transaction with ID: {TransactionId}", 
                LogSanitizer.MaskTransferId(id));

            return await _context.Transactions.FindAsync(id);
        }

        public async Task<Transaction?> GetTransactionByTransferIdAsync(string transferId)
        {
            // Sanitize ID for logging
            _logger.LogInformation("Getting transaction with transfer ID: {TransferId}", 
                LogSanitizer.MaskTransferId(transferId));

            return await _context.Transactions
                .FirstOrDefaultAsync(t => t.TransferId == transferId);
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(int userId)
        {
            // Mask user ID for logging
            _logger.LogInformation("Getting transactions for user: {UserId}", 
                LogSanitizer.MaskAccountId(userId));

            return await _context.Transactions
                .Where(t => t.UserId == userId)
                .ToListAsync();
        }

        public async Task<Transaction> UpdateTransactionStatusAsync(string id, string status)
        {
            // Secure logging
            _logger.LogInformation("Updating transaction {TransactionId} status to: {Status}", 
                LogSanitizer.MaskTransferId(id), status);

            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction with ID {LogSanitizer.MaskTransferId(id)} not found");
            }

            transaction.Status = status;
            transaction.UpdatedAt = DateTime.UtcNow;
            transaction.LastModifiedBy = "system"; // Record who made the change
            
            await _context.SaveChangesAsync();

            // Log the status change with secure logger
            await _secureLogger.LogTransactionEventAsync(
                id,
                "status_change",
                $"Transaction status updated to: {status}");

            return transaction;
        }

        public async Task<IEnumerable<TransactionLog>> GetTransactionLogsAsync(string transactionId)
        {
            // Secure logging
            _logger.LogInformation("Getting logs for transaction: {TransactionId}", 
                LogSanitizer.MaskTransferId(transactionId));

            // Return logs with sanitized messages if they contain sensitive data
            var logs = await _context.TransactionLogs
                .Where(l => l.TransactionId == transactionId)
                .OrderBy(l => l.CreatedAt)
                .Select(l => new TransactionLog 
                {
                    Id = l.Id,
                    TransactionId = l.TransactionId,
                    LogType = l.LogType,
                    // Use sanitized message if contains sensitive data
                    Message = l.ContainsSensitiveData ? l.SanitizedMessage ?? l.Message : l.Message,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync();

            return logs;
        }

        public async Task AddTransactionLogAsync(string transactionId, string logType, string message)
        {
            // Use the secure logger instead of directly adding logs
            await _secureLogger.LogTransactionEventAsync(transactionId, logType, message);
        }
    }
}
