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
            timestamp = DateTime.UtcNow,
            isDelayed = false // This is an immediate check since the service IS available
        };

        // Queue the message for fraud check
        try {
            string messageJson = JsonSerializer.Serialize(fraudMessage);
            logger.LogInformation("Sending immediate fraud check message for {TransferId}", transferId);
            
            using var channel = rabbitMqClient.CreateChannel();
            var body = System.Text.Encoding.UTF8.GetBytes(messageJson);
            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            
            channel.BasicPublish(
                exchange: "",
                routingKey: "CheckFraud",
                basicProperties: props,
                body: body
            );
            
            logger.LogInformation("Published immediate fraud check request for {TransferId}", transferId);
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to publish fraud check request: {Error}", ex.Message);
            throw new ServiceUnavailableException("FraudDetectionService", "Failed to send fraud check request");
        }

        // Wait for the result to appear in Redis (with timeout)
        logger.LogInformation("Waiting for fraud check result for {TransferId}", transferId);
        
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                10, // Try 10 times
                retryAttempt => TimeSpan.FromMilliseconds(500), // Wait 500ms between retries
                (exception, timeSpan, retryCount, context) => {
                    logger.LogDebug("Waiting for fraud result {TransferId} (attempt {RetryCount})", transferId, retryCount);
                });

        try
        {
            return await retryPolicy.ExecuteAsync(async () =>
            {
                var result = await redisClient.GetAsync($"fraud:result:{transferId}");
                if (string.IsNullOrEmpty(result))
                {
                    throw new InvalidOperationException("Fraud check result not yet available");
                }
                
                logger.LogInformation("Received fraud check result for {TransferId}", transferId);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new DateTimeJsonConverter() }
                };
                return JsonSerializer.Deserialize<FraudResult>(result, options) ?? throw new InvalidOperationException();
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Timeout waiting for fraud check result for {TransferId}", transferId);
            throw new ServiceUnavailableException("FraudDetectionService", "Timeout waiting for fraud check result");
        }
    }
}
