using TransactionService.Models;

namespace TransactionService.Services.Interface;

public interface IFraudDetectionService
{
    Task<bool> IsServiceAvailableAsync();
    Task<FraudResult> CheckFraudAsync(string transferId, Transaction transaction);
}