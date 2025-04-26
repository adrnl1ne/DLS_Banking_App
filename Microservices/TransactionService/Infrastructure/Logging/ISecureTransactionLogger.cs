using System.Threading.Tasks;

namespace TransactionService.Infrastructure.Logging
{
    public interface ISecureTransactionLogger
    {
        Task LogTransactionEventAsync(string transactionId, string eventType, string message);
    }
}