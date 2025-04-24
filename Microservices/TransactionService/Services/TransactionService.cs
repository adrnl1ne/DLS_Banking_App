using Prometheus;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Models;

namespace TransactionService.Services
{
    public class TransactionService(
        ITransactionRepository repository,
        UserAccountClientService userAccountClient,
        ILogger<TransactionService> logger,
        Histogram histogram)
        : ITransactionService
    {
        public async Task<TransactionResponse> CreateTransferAsync(TransactionRequest request)
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

        public async Task<TransactionResponse> GetTransactionByTransferIdAsync(string transferId)
        {
            var transaction = await repository.GetTransactionByTransferIdAsync(transferId);
            return (transaction != null ? TransactionResponse.FromTransaction(transaction) : null) ?? throw new InvalidOperationException();
        }

        public async Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId, int authenticatedUserId)
        {
            try
            {
                // Validate accountId format
                if (!int.TryParse(accountId, out int accountIdInt))
                {
                    logger.LogWarning("Invalid account ID format: {AccountId}", accountId);
                    throw new ArgumentException("Account ID must be a valid integer.");
                }

                // Call UserAccountService to get account details
                logger.LogInformation("Fetching account {AccountId} from UserAccountService", accountId);
                var account = await userAccountClient.GetAccountAsync(accountIdInt);

                if (account == null)
                {
                    logger.LogWarning("Account {AccountId} not found in UserAccountService", accountId);
                    throw new InvalidOperationException($"Account {accountId} not found.");
                }

                // Validate that the authenticated user owns the account
                if (account.UserId != authenticatedUserId)
                {
                    logger.LogWarning("User {UserId} is not authorized to access transactions for account {AccountId}", authenticatedUserId, accountId);
                    throw new UnauthorizedAccessException("You are not authorized to access transactions for this account.");
                }

                // Fetch transactions from the repository
                var transactions = await repository.GetTransactionsByAccountAsync(accountId);

                return transactions.Select(TransactionResponse.FromTransaction);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw; // Re-throw to let the controller handle it
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving transactions for account {AccountId}", accountId);
                throw;
            }
        }
    }
}
