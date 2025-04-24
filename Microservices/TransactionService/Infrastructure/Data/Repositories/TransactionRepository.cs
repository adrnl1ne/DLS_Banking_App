using Microsoft.EntityFrameworkCore;
using TransactionService.Models;

namespace TransactionService.Infrastructure.Data.Repositories;

public class TransactionRepository(TransactionDbContext context, ILogger<TransactionRepository> logger)
    : ITransactionRepository
{
    public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
    {
        try
        {
            await context.Transactions.AddAsync(transaction);
            await context.SaveChangesAsync();
            return transaction;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating transaction: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<Transaction?> GetTransactionByIdAsync(Guid id)
    {
        return await context.Transactions.FindAsync(id);
    }

    public async Task<Transaction?> GetTransactionByTransferIdAsync(string transferId)
    {
        try
        {
            var sanitizedTransferId = transferId.Replace("\n", "").Replace("\r", "");
            logger.LogInformation("Getting transaction with transfer ID: {TransferId}", sanitizedTransferId);

            var transaction = await context.Transactions
                .AsQueryable() // Explicitly treat as IQueryable
                .FirstOrDefaultAsync(t => t.TransferId == transferId);

            if (transaction == null)
            {
                logger.LogWarning("No transaction found with transfer ID: {TransferId}", sanitizedTransferId);
                return null;
            }

            logger.LogInformation("Found transaction with ID: {Id}", transaction.Id);
            return transaction;
        }
        catch (Exception ex)
        {
            var logSafeTransferId = transferId.Replace("\n", "").Replace("\r", "");
            logger.LogError(ex, "Error getting transaction with transfer ID: {TransferId}. Error: {Message}", 
                logSafeTransferId, ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByAccountAsync(string accountId)
    {
        try
        {
            var sanitizedAccountId = accountId.Replace("\n", "").Replace("\r", "");
            logger.LogInformation("Getting transactions for account: {AccountId}", sanitizedAccountId);

            if (!int.TryParse(accountId, out int accountIdInt))
            {
                throw new ArgumentException("Account ID must be a valid integer.");
            }

            var transactions = await context.Transactions
                .AsQueryable()
                .Where(t => t.FromAccount == accountIdInt.ToString() || t.ToAccount == accountIdInt.ToString())
                .ToListAsync();

            logger.LogInformation("Found {Count} transactions for account: {AccountId}", transactions.Count, sanitizedAccountId);
            return transactions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting transactions for account: {AccountId}. Error: {Message}", accountId, ex.Message);
            throw;
        }
    }

    public async Task<Transaction> UpdateTransactionStatusAsync(string transferId, string status)
    {
        try
        {
            logger.LogInformation("Updating transaction {TransferId} status to {Status}", transferId, status);

            var transaction = await context.Transactions
                .AsQueryable() // Explicitly treat as IQueryable
                .FirstOrDefaultAsync(t => t.TransferId == transferId);

            if (transaction == null)
            {
                logger.LogWarning("No transaction found with transfer ID: {TransferId}", transferId);
                throw new KeyNotFoundException($"Transaction with ID {transferId} not found");
            }

            transaction.Status = status;
            transaction.UpdatedAt = DateTime.UtcNow;

            context.Transactions.Update(transaction);
            await context.SaveChangesAsync();

            logger.LogInformation("Updated transaction: {Id}", transaction.Id);
            return transaction;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating transaction status for transfer ID: {TransferId}. Error: {Message}", 
                transferId, ex.Message);
            throw;
        }
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await context.SaveChangesAsync() > 0;
    }
}
