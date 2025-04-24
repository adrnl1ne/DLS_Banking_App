using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prometheus;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;

namespace TransactionService.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _repository;
        private readonly IRabbitMQClient _rabbitMqClient;
        private readonly UserAccountClientService _userAccountClient;
        private readonly ILogger<TransactionService> _logger;
        private readonly Counter _counter;
        private readonly Histogram _histogram;

        public TransactionService(
            ITransactionRepository repository,
            IRabbitMQClient rabbitMqClient,
            UserAccountClientService userAccountClient,
            ILogger<TransactionService> logger,
            Counter counter,
            Histogram histogram)
        {
            _repository = repository;
            _rabbitMqClient = rabbitMqClient;
            _userAccountClient = userAccountClient;
            _logger = logger;
            _counter = counter;
            _histogram = histogram;
        }

        public async Task<TransactionResponse> CreateTransferAsync(TransactionRequest request)
        {
            try
            {
                _logger.LogInformation($"Creating transfer from {request.FromAccount} to {request.ToAccount} for {request.Amount}");

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

                await _repository.CreateTransactionAsync(transaction);

                // Convert decimal to double for Histogram
                _histogram.Observe((double)transaction.Amount);

                return TransactionResponse.FromTransaction(transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transfer");
                throw;
            }
        }

        public async Task<TransactionResponse> GetTransactionByTransferIdAsync(string transferId)
        {
            var transaction = await _repository.GetTransactionByTransferIdAsync(transferId);
            return (transaction != null ? TransactionResponse.FromTransaction(transaction) : null) ?? throw new InvalidOperationException();
        }

        public async Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId, int authenticatedUserId)
        {
            try
            {
                // Validate accountId format
                if (!int.TryParse(accountId, out int accountIdInt))
                {
                    _logger.LogWarning("Invalid account ID format: {AccountId}", accountId);
                    throw new ArgumentException("Account ID must be a valid integer.");
                }

                // Call UserAccountService to get account details
                _logger.LogInformation("Fetching account {AccountId} from UserAccountService", accountId);
                var account = await _userAccountClient.GetAccountAsync(accountIdInt);

                if (account == null)
                {
                    _logger.LogWarning("Account {AccountId} not found in UserAccountService", accountId);
                    throw new InvalidOperationException($"Account {accountId} not found.");
                }

                // Validate that the authenticated user owns the account
                if (account.UserId != authenticatedUserId)
                {
                    _logger.LogWarning("User {UserId} is not authorized to access transactions for account {AccountId}", authenticatedUserId, accountId);
                    throw new UnauthorizedAccessException("You are not authorized to access transactions for this account.");
                }

                // Fetch transactions from the repository
                var transactions = await _repository.GetTransactionsByAccountAsync(accountId);

                return transactions.Select(TransactionResponse.FromTransaction);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw; // Re-throw to let the controller handle it
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving transactions for account {AccountId}", accountId);
                throw;
            }
        }
    }
}
