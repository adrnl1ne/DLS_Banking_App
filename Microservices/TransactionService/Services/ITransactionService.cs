using System.Collections.Generic;
using System.Threading.Tasks;
using TransactionService.Models;

namespace TransactionService.Services
{
    public interface ITransactionService
    {
        Task<TransactionResponse> CreateTransferAsync(TransactionRequest request);
        Task<TransactionResponse> GetTransactionByTransferIdAsync(string transferId);
        Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId, int authenticatedUserId);
    }
}
