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
            logger.LogInformation("Checking fraud detection service health at: {HealthUrl}", $"{client.BaseAddress}health");
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
    // Prepare and send the fraud check message
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

    // Send the message to fraud detection service
    rabbitMqClient.Publish("CheckFraud", JsonSerializer.Serialize(fraudMessage));

    // Poll Redis for the result
    var retryPolicy = Policy
        .HandleResult<FraudResult>(result => result == null)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: _ => TimeSpan.FromSeconds(2),
            onRetry: (_, _, retryCount, _) =>
            {
                logger.LogWarning("Retry {RetryCount} for fraud check on transaction {TransferId}: Result not found in Redis",
                    retryCount, transferId);
            });

    FraudResult fraudResult;
    try
    {
        fraudResult = await retryPolicy.ExecuteAsync(async () =>
        {
            var resultJson = await redisClient.GetAsync($"fraud:result:{transferId}");
            if (string.IsNullOrEmpty(resultJson))
            {
                throw new ArgumentException("Result not found in Redis");
            }

            // Configure JsonSerializer to use camelCase
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Deserialize<FraudResult>(resultJson, options) ?? throw new InvalidOperationException();
        });

        if (fraudResult == null)
        {
            throw new InvalidOperationException($"Fraud check failed after multiple attempts for transaction {transferId}");
        }

        logger.LogInformation("Received fraud check result for transaction {TransferId}: IsFraud={IsFraud}, Status={Status}",
            fraudResult.TransferId, fraudResult.IsFraud, fraudResult.Status);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Fraud check failed after retries for transaction {TransferId}", transferId);
        throw;
    }

    return fraudResult;
}
}