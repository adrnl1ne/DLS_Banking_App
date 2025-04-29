using System.Diagnostics.Metrics;
using Polly;
using Prometheus;
using TransactionService.Models;
using TransactionService.Services.Interface;

namespace TransactionService.Services;

public class TransactionValidator(
    ILogger<TransactionValidator> logger,
    IUserAccountClient userAccountClient,
    IHttpClientFactory httpClientFactory,
    Counter errorsTotal)
{

    public async Task<bool> IsUserAccountServiceAvailableAsync()
    {
        try
        {
            var client = httpClientFactory.CreateClient("UserAccountClient");
            var response = await client.GetAsync("/api/health");
            response.EnsureSuccessStatusCode();
            logger.LogInformation("User account service health check passed");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "User account service is unavailable");
            return false;
        }
    }

    public async Task<(Account FromAccount, Account ToAccount)> ValidateTransferRequestAsync(TransactionRequest request)
    {
        // Parse account IDs to integers
        if (!int.TryParse(request.FromAccount, out int fromAccountId) ||
            !int.TryParse(request.ToAccount, out int toAccountId))
        {
            logger.LogWarning("Invalid account ID format");
            errorsTotal.WithLabels("CreateTransfer").Inc();
            throw new ArgumentException("Account IDs must be valid integers");
        }

        // Check if from and to accounts are the same
        if (fromAccountId == toAccountId)
        {
            logger.LogWarning("Cannot transfer to the same account - AccountId: {FromAccountId}", fromAccountId);
            errorsTotal.WithLabels("CreateTransfer").Inc();
            throw new InvalidOperationException("Cannot transfer funds to the same account");
        }

        // Define a retry policy for user account service calls
        var userAccountRetryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3, // Retry 3 times
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(2), // Wait 2 seconds between retries
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning("Retry {RetryCount} for user account service call due to {ExceptionMessage}",
                        retryCount, exception.Message);
                });

        // Check if the user account service is available (simplified health check)
        async Task<bool> IsUserAccountServiceAvailable()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await userAccountClient.GetAccountAsync(fromAccountId);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "User account service is unavailable");
                return false;
            }
        }

        // Verify user account service availability
        if (!await IsUserAccountServiceAvailable())
        {
            logger.LogWarning("User account service is down, rejecting transaction");
            errorsTotal.WithLabels("CreateTransfer").Inc();
            throw new InvalidOperationException("Something went wrong, please try again later.");
        }

        // Fetch source account with retry
        var fromAccount = await userAccountRetryPolicy.ExecuteAsync(async () =>
            await userAccountClient.GetAccountAsync(fromAccountId));

        if (fromAccount == null)
        {
            logger.LogWarning("Source account not found");
            errorsTotal.WithLabels("CreateTransfer").Inc();
            throw new InvalidOperationException("Source account not found");
        }

        // Check if user owns the source account
        if (fromAccount.UserId != request.UserId)
        {
            logger.LogWarning("User does not own the source account");
            errorsTotal.WithLabels("CreateTransfer").Inc();
            throw new UnauthorizedAccessException("You can only transfer funds from your own accounts");
        }

        // Check sufficient balance for withdrawal/transfer
        if (fromAccount.Amount < request.Amount)
        {
            logger.LogWarning("Insufficient funds in account {FromAccountId}", fromAccountId);
            errorsTotal.WithLabels("CreateTransfer").Inc();
            throw new InvalidOperationException("Insufficient funds for this transfer");
        }

        // Fetch destination account with retry
        var toAccount = await userAccountRetryPolicy.ExecuteAsync(async () =>
            await userAccountClient.GetAccountAsync(toAccountId));

        if (toAccount == null)
        {
            logger.LogWarning("Destination account not found");
            errorsTotal.WithLabels("CreateTransfer").Inc();
            throw new InvalidOperationException($"Destination account {toAccountId} not found");
        }

        return (fromAccount, toAccount);
    }
}