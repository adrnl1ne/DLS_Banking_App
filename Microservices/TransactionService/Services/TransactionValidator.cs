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
        // We always return true to allow queueing when UserAccountService is down
        logger.LogInformation("Skipping UserAccountService availability check to allow transaction queueing");
        return true;
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
                retryCount: 2, // Reduced retries to fail faster
                sleepDurationProvider: _ => TimeSpan.FromSeconds(1),
                onRetry: (exception, _, retryCount, _) =>
                {
                    logger.LogWarning("Retry {RetryCount} for user account service call due to {ExceptionMessage}",
                        retryCount, exception.Message);
                });

        try
        {
            // Try to fetch accounts but don't fail the transaction if unavailable
            Account fromAccount, toAccount;
            
            try
            {
                // Try to fetch source account with retry
                fromAccount = await userAccountRetryPolicy.ExecuteAsync(async () =>
                    await userAccountClient.GetAccountAsync(fromAccountId));
                
                // Try to fetch destination account with retry
                toAccount = await userAccountRetryPolicy.ExecuteAsync(async () =>
                    await userAccountClient.GetAccountAsync(toAccountId)); 
                    
                // Validate accounts are valid if we got them
                if (fromAccount == null || toAccount == null)
                {
                    logger.LogWarning("Source or destination account not found, but proceeding for queueing");
                    // Create placeholder accounts with minimal data needed for queueing
                    fromAccount ??= new Account { Id = fromAccountId, UserId = request.UserId };
                    toAccount ??= new Account { Id = toAccountId, UserId = 0 }; // Unknown user ID
                }
                else
                {
                    // Only validate ownership and balance if we got actual account data
                    if (fromAccount.UserId != request.UserId)
                    {
                        logger.LogWarning("User does not own the source account");
                        errorsTotal.WithLabels("CreateTransfer").Inc();
                        throw new UnauthorizedAccessException("You can only transfer funds from your own accounts");
                    }

                    if (fromAccount.Amount < request.Amount)
                    {
                        logger.LogWarning("Insufficient funds in user account");
                        errorsTotal.WithLabels("CreateTransfer").Inc();
                        throw new InvalidOperationException("Insufficient funds for this transfer");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error fetching account details, creating placeholder accounts for queueing");
                // Create placeholder accounts with minimal data needed for queueing
                fromAccount = new Account { Id = fromAccountId, UserId = request.UserId };
                toAccount = new Account { Id = toAccountId, UserId = 0 }; // Unknown user ID
            }

            return (fromAccount, toAccount);
        }
        catch (Exception ex)
        {
            errorsTotal.WithLabels("CreateTransfer").Inc();
            logger.LogError(ex, "Error validating transfer request");
            throw;
        }
    }
}