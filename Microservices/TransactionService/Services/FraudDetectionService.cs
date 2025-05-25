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
    private readonly IRabbitMQClient _rabbitMqClient;
    private readonly FraudResultConsumer _fraudResultConsumer;

    public FraudDetectionService(
        ILogger<FraudDetectionService> logger,
        IRabbitMQClient rabbitMqClient,
        FraudResultConsumer fraudResultConsumer)
    {
        _logger = logger;
        _rabbitMqClient = rabbitMqClient;
        _fraudResultConsumer = fraudResultConsumer;
    }

    public async Task<bool> IsServiceAvailableAsync()
    {
        // Can check if RabbitMQ connection is alive
        return true;
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

        // Publish message to CheckFraud queue
        _rabbitMqClient.Publish("CheckFraud", JsonSerializer.Serialize(fraudMessage));

        try
        {
            // Wait for response from FraudResult queue with timeout
            var fraudResult = await _fraudResultConsumer.WaitForResult(transferId, TimeSpan.FromSeconds(30));

            _logger.LogInformation(
                "Received fraud check result for transaction {TransferId}: IsFraud={IsFraud}, Status={Status}",
                fraudResult.TransferId, fraudResult.IsFraud, fraudResult.Status);

            return fraudResult;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Fraud check timed out for transaction");

            // Return default result in case of timeout
            return new FraudResult
            {
                TransferId = transferId,
                IsFraud = false,
                Status = "approved",
                Amount = transaction.Amount,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}
