using System.Text.Json;
using Polly;
using RabbitMQ.Client;
using TransactionService.Exceptions;
using TransactionService.Infrastructure.Json;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Infrastructure.Redis;
using TransactionService.Models;
using TransactionService.Services.Interface;

namespace TransactionService.Services;

public class FraudDetectionService : IFraudDetectionService
{
    private readonly ILogger<FraudDetectionService> _logger;
    private readonly IRabbitMqClient _rabbitMqClient;

    public FraudDetectionService(
        ILogger<FraudDetectionService> logger,
        IRabbitMqClient rabbitMqClient)
    {
        _logger = logger;
        _rabbitMqClient = rabbitMqClient;
    }

    public async Task<bool> IsServiceAvailableAsync()
    {
        // Check if RabbitMQ connection is alive
        try
        {
            return _rabbitMqClient.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    public async Task<FraudResult> CheckFraudAsync(string transferId, Transaction transaction)
    {
        _logger.LogInformation("Sending transaction to fraud check");

        // Create the message
        var fraudMessage = new
        {
            transferId = transaction.TransferId,
            fromAccount = transaction.FromAccount,
            toAccount = transaction.ToAccount,
            amount = transaction.Amount,
            userId = transaction.UserId,
            timestamp = DateTime.UtcNow
        };

        try
        {
            // Publish message to CheckFraud queue
            _rabbitMqClient.Publish("CheckFraud", JsonSerializer.Serialize(fraudMessage));
            
            _logger.LogInformation("Published fraud check request for {TransferId}", transferId);

            // For now, return a default approved result since we're handling results asynchronously
            // The actual result will be processed by FraudResultConsumer
            return new FraudResult
            {
                TransferId = transferId,
                IsFraud = false,
                Status = "approved",
                Amount = transaction.Amount,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send fraud check request");
            throw new ServiceUnavailableException("Failed to send fraud check request", "Bæ");
        }
    }
}
