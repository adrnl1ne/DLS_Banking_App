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
        private readonly ILogger<TransactionService> _logger;
        private readonly Counter _counter;
        private readonly Histogram _histogram;

        public TransactionService(
            ITransactionRepository repository,
            IRabbitMQClient rabbitMqClient,
            ILogger<TransactionService> logger,
            Counter counter,
            Histogram histogram)
        {
            _repository = repository;
            _rabbitMqClient = rabbitMqClient;
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
            return transaction != null ? TransactionResponse.FromTransaction(transaction) : null;
        }

        public async Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId)
        {
            var transactions = await _repository.GetTransactionsByAccountAsync(accountId);
            return transactions?.Select(TransactionResponse.FromTransaction);
        }
    }
}
