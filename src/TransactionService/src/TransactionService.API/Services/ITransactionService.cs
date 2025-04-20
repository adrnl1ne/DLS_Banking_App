using TransactionService.API.Models;

namespace TransactionService.API.Services;

public interface ITransactionService
{
    Task<TransactionResponse> CreateTransferAsync(TransactionRequest request);
    Task<TransactionResponse?> GetTransactionByTransferIdAsync(string transferId);
    Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId);
    Task HandleFraudDetectionResultAsync(string transferId, bool isFraud, string status);
}