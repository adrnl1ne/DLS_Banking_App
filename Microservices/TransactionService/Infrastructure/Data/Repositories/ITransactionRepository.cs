using TransactionService.Models;

namespace TransactionService.Infrastructure.Data.Repositories;

public interface ITransactionRepository
{
    Task<Transaction> CreateTransactionAsync(Transaction transaction);
    Task<Transaction?> GetTransactionByIdAsync(string id);  // Changed from Guid to string
    Task<Transaction?> GetTransactionByTransferIdAsync(string transferId);
    Task<IEnumerable<Transaction>> GetTransactionsByAccountAsync(string accountId);
    Task<Transaction> UpdateTransactionStatusAsync(string transferId, string status);
    Task<bool> SaveChangesAsync();
}
