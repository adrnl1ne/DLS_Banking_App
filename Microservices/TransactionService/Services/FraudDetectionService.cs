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

public class FraudDetectionService(
    ILogger<FraudDetectionService> logger,
    IRabbitMqClient rabbitMqClient,
    IHttpClientFactory httpClientFactory,
    IRedisClient redisClient)
    : IFraudDetectionService
{
    public async Task<bool> IsServiceAvailableAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("FraudDetectionClient");
            logger.LogInformation("Checking fraud detection service health");
            var response = await client.GetAsync("/health");
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Fraud detection service health check passed");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fraud detection service is unavailable");
            return false;
        }
    }

    public async Task<FraudResult> CheckFraudAsync(string transferId, Transaction transaction)
    {
        logger.LogInformation("Sending transaction {TransferId} for fraud check", transferId);
        
        // First check if result already exists in Redis (for idempotence)
        var resultJson = await redisClient.GetAsync($"fraud:result:{transferId}");
        if (!string.IsNullOrEmpty(resultJson))
        {
            logger.LogInformation("Found existing fraud check result for {TransferId}", transferId);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new DateTimeJsonConverter() }
            };
            return JsonSerializer.Deserialize<FraudResult>(resultJson, options) ?? throw new InvalidOperationException();
        }

        var fraudMessage = new
        {
            transferId = transaction.TransferId,
            fromAccount = transaction.FromAccount,
            toAccount = transaction.ToAccount,
            amount = transaction.Amount,
            userId = transaction.UserId,
            timestamp = DateTime.UtcNow
        };

        // Queue the message for fraud check - WITHOUT creating the queue
        try {
            string messageJson = JsonSerializer.Serialize(fraudMessage);
            logger.LogInformation("Serialized fraud check message: {Message}", messageJson);
            
            // Use default/existing queue settings - DON'T try to declare or modify the queue
            using var channel = rabbitMqClient.CreateChannel();
            var body = System.Text.Encoding.UTF8.GetBytes(messageJson);
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            
            // Just publish directly to the existing queue
            channel.BasicPublish(
                exchange: "",
                routingKey: "CheckFraud",
                basicProperties: props,
                body: body
            );
            
            logger.LogInformation("Published fraud check request for {TransferId}", transferId);
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to publish fraud check request: {Error}", ex.Message);
            throw new ServiceUnavailableException("FraudDetectionService", "Failed to send fraud check request");
        }

        logger.LogInformation("Creating placeholder fraud result while waiting for async processing");
        
        // Return a pending placeholder result - we'll process this async
        var pendingResult = new FraudResult
        {
            TransferId = transferId,
            IsFraud = false, 
            Status = "pending",
            Timestamp = DateTime.UtcNow
        };
        
        return pendingResult;
    }
}
