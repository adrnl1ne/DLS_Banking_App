using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Security;
using TransactionService.Models;

namespace TransactionService.Infrastructure.Data.Repositories
{
    public class TransactionRepository(
        TransactionDbContext context,
        ILogger<TransactionRepository> logger,
        Logging.ISecureTransactionLogger secureLogger)
        : ITransactionRepository
    {
        public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
        {
            // Store ClientIp temporarily if needed for logging
            var clientIp = transaction.ClientIp;
            
            // Save transaction
            var result = await context.Transactions.AddAsync(transaction);
            await context.SaveChangesAsync();

            // Log the IP address securely without storing it in the database
            if (!string.IsNullOrEmpty(clientIp))
            {
                await secureLogger.LogTransactionEventAsync(transaction.Id, "ClientInfo",
                    $"Request originated from IP: {clientIp}");
            }
            
            // Return the full transaction object, not just the ID
            return transaction;
        }

        public async Task<Transaction?> GetTransactionByIdAsync(string id)
        {
            return await context.Transactions.FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Transaction?> GetTransactionByTransferIdAsync(string transferId)
        {
            return await context.Transactions.FirstOrDefaultAsync(t => t.TransferId == transferId);
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(int userId)
        {
            // Mask user ID for logging
            logger.LogInformation("Getting transactions for user: {UserId}",
                LogSanitizer.MaskAccountId(userId));

            return await context.Transactions
                .Where(t => t.UserId == userId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByAccountAsync(string accountId)
        {
            // Mask account ID for logging
            logger.LogInformation("Getting transactions for account: {AccountId}",
                LogSanitizer.MaskTransferId(accountId));

            // Get transactions where the account is either the source or the destination
            return await context.Transactions
                .Where(t => t.FromAccount == accountId || t.ToAccount == accountId)
                .ToListAsync();
        }

        public async Task<Transaction> UpdateTransactionStatusAsync(string id, string status)
        {
            // Secure logging
            logger.LogInformation("Updating transaction {TransactionId} status to: {Status}",
                LogSanitizer.MaskTransferId(id), status);

            var transaction = await context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                throw new KeyNotFoundException($"Transaction with ID {LogSanitizer.MaskTransferId(id)} not found");
            }

            transaction.Status = status;
            transaction.UpdatedAt = DateTime.UtcNow;
            // Set this for use in the application but it won't be saved to DB
            transaction.LastModifiedBy = "system";
            
            await context.SaveChangesAsync();

            // Log the status change with secure logger
            await secureLogger.LogTransactionEventAsync(
                id,
                "status_change",
                $"Transaction status updated to: {status}");

            return transaction;
        }

        public async Task<IEnumerable<TransactionLog>> GetTransactionLogsAsync(string transactionId)
        {
            // Secure logging
            logger.LogInformation("Getting logs for transaction: {TransactionId}",
                LogSanitizer.MaskTransferId(transactionId));

            // Return logs with sanitized messages if they contain sensitive data
            var logs = await context.TransactionLogs
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
            await secureLogger.LogTransactionEventAsync(transactionId, logType, message);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await context.SaveChangesAsync();
        }
    }
}
