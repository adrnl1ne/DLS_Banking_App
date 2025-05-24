using System.Collections.Generic;
using System.Threading.Tasks;
using TransactionService.Models;

namespace TransactionService.Infrastructure.Data.Repositories
{
    public interface ITransactionRepository
    {
        Task<Transaction> CreateTransactionAsync(Transaction transaction);
        Task<Transaction?> GetTransactionByIdAsync(string id);
        Task<Transaction?> GetTransactionByTransferIdAsync(string transferId);
        Task<IEnumerable<Transaction>> GetTransactionsByUserIdAsync(int userId);
        Task<IEnumerable<Transaction>> GetTransactionsByAccountAsync(string accountId);
        Task<Transaction> UpdateTransactionStatusAsync(string id, string status);
        Task<IEnumerable<TransactionLog>> GetTransactionLogsAsync(string transactionId);
        Task AddTransactionLogAsync(string transactionId, string logType, string message);
        Task<int> SaveChangesAsync();
        Task<Transaction> UpdateTransactionAsync(Transaction transaction);
    }
}
