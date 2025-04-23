using System.Collections.Generic;
using System.Threading.Tasks;
using TransactionService.API.Models;

namespace TransactionService.API.Infrastructure.Data.Repositories;

public interface ITransactionRepository
{
    Task<Transaction> CreateTransactionAsync(Transaction transaction);
    Task<Transaction?> GetTransactionByTransferIdAsync(string transferId);
    Task<IEnumerable<Transaction>> GetTransactionsByAccountAsync(string accountId);
    
    // The interface expects this method to return Task<Transaction>
    Task<Transaction> UpdateTransactionStatusAsync(string transferId, string status);
}
