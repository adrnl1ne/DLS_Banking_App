using TransactionService.Models;

namespace TransactionService.Services.Interface
{
    public interface ITransactionService
    {
        Task<TransactionResponse> CreateTransferAsync(TransactionRequest request);
        Task<TransactionResponse?> GetTransactionByTransferIdAsync(string transferId);
        Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId, int authenticatedUserId);
        Task HandleBalanceUpdateConfirmationAsync(string transferId, string transactionType, bool success);
    }
}
