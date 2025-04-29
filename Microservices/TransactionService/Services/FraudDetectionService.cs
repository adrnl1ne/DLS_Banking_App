using System.Text.Json;
using Polly;
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
            logger.LogInformation("Checking fraud detection service health at: {HealthUrl}",
                $"{client.BaseAddress}health");
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
        logger.LogInformation("Sending transaction {TransferId} to fraud check", transferId);
        var fraudMessage = new
        {
            transferId = transaction.TransferId,
            fromAccount = transaction.FromAccount,
            toAccount = transaction.ToAccount,
            amount = transaction.Amount,
            userId = transaction.UserId,
            timestamp = DateTime.UtcNow
        };

        rabbitMqClient.Publish("CheckFraud", JsonSerializer.Serialize(fraudMessage));

        var retryPolicy = Policy
            .HandleResult<FraudResult>(result => result == null)
            .WaitAndRetryAsync(
                retryCount: 5, // Increased from 3 to 5
                sleepDurationProvider: _ => TimeSpan.FromSeconds(3), // Increased from 2s to 3s
                onRetry: (_, _, retryCount, _) =>
                {
                    logger.LogWarning(
                        "Retry {RetryCount} for fraud check on transaction {TransferId}: Result not found in Redis",
                        retryCount, transferId);
                });

        FraudResult? fraudResult = null;
        try
        {
            fraudResult = await retryPolicy.ExecuteAsync(async () =>
            {
                var resultJson = await redisClient.GetAsync($"fraud:result:{transferId}");
                if (string.IsNullOrEmpty(resultJson))
                {
                    logger.LogDebug("No fraud result found in Redis for key fraud:result:{TransferId}", transferId);
                    return null;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                return JsonSerializer.Deserialize<FraudResult>(resultJson, options);
            });

            if (fraudResult == null)
            {
                logger.LogWarning(
                    "Fraud check result not found after retries for transaction {TransferId}. Assuming non-fraudulent for development.",
                    transferId);
                fraudResult = new FraudResult
                {
                    TransferId = transferId,
                    IsFraud = false,
                    Status = "approved",
                    Timestamp = DateTime.UtcNow
                };
            }

            logger.LogInformation(
                "Received fraud check result for transaction {TransferId}: IsFraud={IsFraud}, Status={Status}",
                fraudResult.TransferId, fraudResult.IsFraud, fraudResult.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fraud check failed for transaction {TransferId}", transferId);
            throw;
        }

        return fraudResult;
    }
}
