using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TransactionService.API.Infrastructure.Data.Repositories;
using TransactionService.API.Infrastructure.Messaging.Events;
using TransactionService.API.Infrastructure.Messaging.RabbitMQ;
using TransactionService.API.Models;
using Prometheus;

namespace TransactionService.API.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _repository;
    private readonly IRabbitMQClient _rabbitMqClient;
    private readonly ILogger<TransactionService> _logger;
    private readonly Counter _transactionCounter;
    private readonly Histogram _transactionAmountHistogram;

    public TransactionService(
        ITransactionRepository repository,
        IRabbitMQClient rabbitMqClient,
        ILogger<TransactionService> logger,
        Counter transactionCounter,
        Histogram transactionAmountHistogram)
    {
        _repository = repository;
        _rabbitMqClient = rabbitMqClient;
        _logger = logger;
        _transactionCounter = transactionCounter;
        _transactionAmountHistogram = transactionAmountHistogram;
    }

    public async Task<TransactionResponse> CreateTransferAsync(TransactionRequest request)
    {
        // Create and save transaction
        var transaction = new Transaction
        {
            TransferId = $"TRX-{Guid.NewGuid():N}",
            FromAccount = request.FromAccount,
            ToAccount = request.ToAccount,
            Amount = request.Amount,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
            // We store UserId in the request but don't need to store it in the transaction
            // It's used for authorization checks
        };

        await _repository.CreateTransactionAsync(transaction);
        
        // Publish transaction created event
        var transactionCreatedEvent = new TransactionCreatedEvent
        {
            TransferId = transaction.TransferId,
            FromAccount = transaction.FromAccount,
            ToAccount = transaction.ToAccount,
            Amount = transaction.Amount,
            Status = transaction.Status,
            CreatedAt = transaction.CreatedAt
        };
        
        _rabbitMqClient.PublishMessage("TransactionCreated", transactionCreatedEvent);
        
        // Send to fraud detection
        var fraudCheckRequest = new
        {
            TransferId = transaction.TransferId,
            Amount = transaction.Amount,
            UserId = request.UserId
        };
        
        _rabbitMqClient.PublishMessage("CheckFraud", fraudCheckRequest);
        
        _logger.LogInformation($"Transaction {transaction.TransferId} created and sent for fraud check");
        
        // Record metrics
        _transactionCounter.WithLabels("created", "transfer").Inc();
        _transactionAmountHistogram.WithLabels("pending").Observe(request.Amount);
        
        return TransactionResponse.FromTransaction(transaction);
    }

    public async Task<TransactionResponse?> GetTransactionByTransferIdAsync(string transferId)
    {
        var transaction = await _repository.GetTransactionByTransferIdAsync(transferId);
        
        if (transaction != null)
        {
            _transactionCounter.WithLabels("retrieved", "query").Inc();
        }
        
        return transaction != null ? TransactionResponse.FromTransaction(transaction) : null;
    }

    public async Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId)
    {
        var transactions = await _repository.GetTransactionsByAccountAsync(accountId);
        
        _transactionCounter.WithLabels("listed", "query").Inc();
        
        return transactions.Select(TransactionResponse.FromTransaction);
    }

    public async Task HandleFraudDetectionResultAsync(string transferId, bool isFraud, string status)
    {
        try
        {
            // Update transaction status based on fraud detection result
            var transaction = await _repository.UpdateTransactionStatusAsync(transferId, status);
            
            _logger.LogInformation($"Transaction {transferId} status updated to {status} (fraud: {isFraud})");
            
            // Publish status updated event
            var statusUpdatedEvent = new TransactionStatusUpdatedEvent
            {
                TransferId = transferId,
                Status = status,
                IsFraud = isFraud,
                UpdatedAt = DateTime.UtcNow
            };
            
            _rabbitMqClient.PublishMessage("TransactionStatusUpdated", statusUpdatedEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating transaction {transferId} status");
            throw;
        }
    }
}
