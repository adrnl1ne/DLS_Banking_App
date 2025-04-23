using Prometheus;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Models;

namespace TransactionService.Services
{
    public class TransactionService(
        ITransactionRepository repository,
        ILogger<TransactionService> logger,
        Histogram histogram)
        : ITransactionService
    {
        public async Task<TransactionResponse?> CreateTransferAsync(TransactionRequest request)
        {
            try
            {
                logger.LogInformation($"Creating transfer from {request.FromAccount} to {request.ToAccount} for {request.Amount}");

                var transaction = new Transaction
                {
                    Id = Guid.NewGuid(),
                    TransferId = $"TRX-{DateTime.UtcNow.Ticks}",
                    UserId = request.UserId,
                    FromAccount = request.FromAccount,
                    ToAccount = request.ToAccount,
                    Amount = request.Amount,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                await repository.CreateTransactionAsync(transaction);

                // Convert decimal to double for Histogram
                histogram.Observe((double)transaction.Amount);

                return TransactionResponse.FromTransaction(transaction);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating transfer");
                throw;
            }
        }

        public async Task<TransactionResponse?> GetTransactionByTransferIdAsync(string transferId)
        {
            var transaction = await repository.GetTransactionByTransferIdAsync(transferId);
            return transaction != null ? TransactionResponse.FromTransaction(transaction) : null;
        }

        public async Task<IEnumerable<TransactionResponse?>> GetTransactionsByAccountAsync(string accountId)
        {
            var transactions = await repository.GetTransactionsByAccountAsync(accountId);
            return transactions.Select(TransactionResponse.FromTransaction);
        }
    }
}
