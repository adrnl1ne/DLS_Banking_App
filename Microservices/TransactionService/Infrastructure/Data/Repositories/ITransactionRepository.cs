using TransactionService.Models;

namespace TransactionService.Infrastructure.Data.Repositories;

public interface ITransactionRepository
{
    Task<Transaction> CreateTransactionAsync(Transaction transaction);
    Task<Transaction?> GetTransactionByTransferIdAsync(string transferId);
    Task<IEnumerable<Transaction>> GetTransactionsByAccountAsync(string accountId);
}
