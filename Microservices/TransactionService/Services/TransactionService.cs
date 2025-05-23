using Newtonsoft.Json;
using Polly;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Models;
using TransactionService.Services.Interface;
using Prometheus;
using TransactionService.Exceptions;
using TransactionService.Infrastructure.Messaging.RabbitMQ;

namespace TransactionService.Services;

public class TransactionService(
    ILogger<TransactionService> logger,
    ITransactionRepository repository,
    IUserAccountClient userAccountClient,
    IFraudDetectionService fraudDetectionService,
    TransactionValidator validator,
    Counter requestsTotal,
    Counter successesTotal,
    Counter errorsTotal,
    IRabbitMqClient rabbitMqClient,
    Histogram histogram,
    IHttpClientFactory httpClientFactory)
    : ITransactionService
{
    public async Task<TransactionResponse> CreateTransferAsync(TransactionRequest request)
    {
        requestsTotal.WithLabels("CreateTransfer").Inc();
        try
        {
            logger.LogInformation("Creating transfer");

            // Check service availability
            bool isUserAccountServiceAvailable = await CheckUserAccountServiceAvailabilityAsync();
            bool isFraudDetectionAvailable = await fraudDetectionService.IsServiceAvailableAsync();

            // Validate the transfer request and fetch accounts
            var (fromAccount, toAccount) = await validator.ValidateTransferRequestAsync(request);

            // Create the pending transaction
            var transaction = await CreatePendingTransactionAsync(request, fromAccount, toAccount, logger, repository);

            // Set initial fraud check status
            bool fraudCheckPending = false;
            bool fraudDetected = false;

            try
            {
                // Always try to perform a synchronous fraud check first
                if (isFraudDetectionAvailable)
                {
                    var fraudResult = await fraudDetectionService.CheckFraudAsync(transaction.TransferId, transaction);

                    // Check both IsFraud and Status to determine if the transaction should be declined
                    if (fraudResult.IsFraud || fraudResult.Status == "declined")
                    {
                        logger.LogWarning("Fraud detected for transaction [ID masked]: Status={Status}", fraudResult.Status);
                        await repository.UpdateTransactionStatusAsync(transaction.Id, "declined");
                        errorsTotal.WithLabels("CreateTransfer").Inc();
                        fraudDetected = true;
                        
                        // Exit early if fraud is detected - don't queue balance updates
                        throw new InvalidOperationException("Transaction declined due to potential fraud");
                    }
                }
                else
                {
                    // FraudDetection service is down, queue the check for later processing
                    logger.LogWarning("Fraud detection service unavailable, queueing request and flagging transaction for review");
                    
                    // Queue the fraud check message for when the service comes back online
                    QueueFraudCheckForLaterProcessing(transaction);
                    
                    // Flag the transaction for review
                    transaction.FraudCheckResult = "pending_review";
                    fraudCheckPending = true;
                    await repository.UpdateTransactionAsync(transaction);
                }
            }
            catch (ServiceUnavailableException ex)
            {
                // Log that fraud detection is down
                logger.LogWarning("Fraud detection service unavailable, proceeding with transaction and flagging for review");

                // Add a flag to the transaction for later review
                transaction.FraudCheckResult = "pending_review";
                fraudCheckPending = true;
                await repository.UpdateTransactionAsync(transaction);
            }

            // Create child transactions (withdrawal and deposit)
            var (withdrawalTransaction, depositTransaction) = await CreateChildTransactionsAsync(
                transaction, fromAccount, toAccount, repository);

            // IMPORTANT: Only queue balance updates if fraud check is NOT pending
            if (!fraudCheckPending)
            {
                // If fraud detection was successful and no fraud was detected, queue the balance updates
                await UpdateAccountBalancesAsync(transaction, fromAccount, toAccount);
            }
            else
            {
                logger.LogInformation("Skipping balance update queueing until fraud check is complete");
            }

            // Update transaction statuses based on processing state
            string transactionStatus = DetermineTransactionStatus(fraudCheckPending, isUserAccountServiceAvailable);
            await UpdateTransactionStatusesAsync(transaction, withdrawalTransaction, depositTransaction,
                repository, transactionStatus);

            logger.LogInformation("Transaction processing completed with status: {Status}", transactionStatus);

            // Track transaction amount in histogram for metrics
            histogram.WithLabels("transfer").Observe((double)transaction.Amount);

            try
            {
                // Serialize the message
                var settings = new JsonSerializerSettings
                {
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc
                };

                var messageJson = JsonConvert.SerializeObject(new
                {
                    transaction.TransferId,
                    transaction.Status,
                    transaction.Amount,
                    transaction.Description,
                    transaction.FromAccount,
                    transaction.ToAccount,
                    transaction.CreatedAt
                }, settings);

                // Publish with queue declaration built-in
                rabbitMqClient.Publish("TransactionCreated", messageJson);
                logger.LogInformation("Published transaction event to TransactionCreated queue");
            }
            catch (Exception ex)
            {
                // Just log the error but don't fail the transaction
                logger.LogError(ex, "Failed to publish transaction event, but transaction was processed successfully");
            }

            // Update status message to reflect the transaction state more accurately
            string statusMessage;
            if (fraudCheckPending) {
                statusMessage = "Transaction is being verified. Balance updates may be delayed due to system maintenance.";
            }
            else if (!isUserAccountServiceAvailable) {
                statusMessage = "Transaction accepted and verified. Balance updates may be delayed due to system maintenance.";
            }
            else {
                statusMessage = "Transaction completed successfully.";
            }

            successesTotal.WithLabels("CreateTransfer").Inc();
            return TransactionResponse.FromTransaction(transaction, statusMessage);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("fraud"))
        {
            // Special handling for fraud detection
            errorsTotal.WithLabels("CreateTransfer").Inc();
            logger.LogWarning("Transaction declined due to fraud detection");
            throw;
        }
        catch (Exception ex)
        {
            errorsTotal.WithLabels("CreateTransfer").Inc();
            logger.LogError(ex, "Error creating transfer");
            throw;
        }
    }

    private static async Task<Transaction> CreatePendingTransactionAsync(
        TransactionRequest request,
        Account fromAccount,
        Account toAccount,
        ILogger<TransactionService> logger,
        ITransactionRepository repository)
    {
        var transferId = Guid.NewGuid().ToString();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            TransferId = transferId,
            UserId = request.UserId,
            FromAccount = request.FromAccount,
            ToAccount = request.ToAccount,
            Amount = request.Amount,
            Status = "pending",
            Description = request.Description ?? $"Transfer from account {fromAccount.Id} to {toAccount.Id}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TransactionType = "Withdrawal",
        };

        await repository.CreateTransactionAsync(transaction);
        logger.LogInformation("Created pending transaction");
        return transaction;
    }

    private static async Task<(Transaction WithdrawalTransaction, Transaction DepositTransaction)>
        CreateChildTransactionsAsync(
            Transaction transaction,
            Account fromAccount,
            Account toAccount,
            ITransactionRepository repository)
    {
        // Create withdrawal transaction for source account
        var withdrawalTransaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            TransferId = transaction.TransferId + "-withdrawal",
            UserId = transaction.UserId,
            FromAccount = transaction.FromAccount,
            ToAccount = transaction.FromAccount,
            Amount = transaction.Amount,
            Status = "pending",
            TransactionType = "withdrawal",
            Description = $"Withdrawal from account {fromAccount.Id} for transfer {transaction.TransferId}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Create deposit transaction for destination account
        var depositTransaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            TransferId = transaction.TransferId + "-deposit",
            UserId = toAccount.UserId,
            FromAccount = transaction.ToAccount,
            ToAccount = transaction.ToAccount,
            Amount = transaction.Amount,
            Status = "pending",
            TransactionType = "deposit",
            Description = $"Deposit to account {toAccount.Id} from transfer {transaction.TransferId}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Save the child transactions
        await repository.CreateTransactionAsync(withdrawalTransaction);
        await repository.CreateTransactionAsync(depositTransaction);

        return (withdrawalTransaction, depositTransaction);
    }

    // Update the UpdateAccountBalancesAsync method to use message queueing

    private async Task UpdateAccountBalancesAsync(
        Transaction transaction,
        Account fromAccount,
        Account toAccount)
    {
        logger.LogInformation("Queueing balance updates for transaction {TransferId}", transaction.TransferId);

        try
        {
            // Create messages for RabbitMQ
            var fromAccountMessage = new AccountBalanceUpdateMessage
            {
                AccountId = fromAccount.Id,
                Amount = transaction.Amount,
                TransactionId = transaction.TransferId + "-withdrawal",
                TransactionType = "Withdrawal",
                IsAdjustment = true,
                Timestamp = DateTime.UtcNow
            };
            
            var toAccountMessage = new AccountBalanceUpdateMessage
            {
                AccountId = toAccount.Id,
                Amount = transaction.Amount,
                TransactionId = transaction.TransferId + "-deposit",
                TransactionType = "Deposit",
                IsAdjustment = true,
                Timestamp = DateTime.UtcNow
            };

            // Prepare the queue - make sure it's declared with consistent settings
            string queueName = "AccountBalanceUpdates";
            try 
            {
                logger.LogInformation("Ensuring queue {QueueName} exists", queueName);
                rabbitMqClient.DeclareQueue(queueName, durable: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error declaring queue {QueueName}, will try to publish anyway", queueName);
            }

            // Serialize to string before publishing
            string fromAccountJson = System.Text.Json.JsonSerializer.Serialize(fromAccountMessage);
            string toAccountJson = System.Text.Json.JsonSerializer.Serialize(toAccountMessage);

            // Log the exact message content for debugging
            logger.LogDebug("Publishing withdrawal message: {Message}", fromAccountJson);
            logger.LogDebug("Publishing deposit message: {Message}", toAccountJson);
            
            // Publish to RabbitMQ queue - this will work even if UserAccountService is down
            rabbitMqClient.Publish(queueName, fromAccountJson);
            logger.LogInformation("Successfully published withdrawal message for account {AccountId}, amount {Amount}", 
                fromAccount.Id, transaction.Amount);
            
            rabbitMqClient.Publish(queueName, toAccountJson);
            logger.LogInformation("Successfully published deposit message for account {AccountId}, amount {Amount}", 
                toAccount.Id, transaction.Amount);
            
            logger.LogInformation("Balance update messages queued successfully for transaction {TransferId}", 
                transaction.TransferId);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue balance updates for transaction {TransferId}", transaction.TransferId);
            throw;
        }
    }

    private static async Task UpdateTransactionStatusesAsync(
        Transaction transaction,
        Transaction withdrawalTransaction,
        Transaction depositTransaction,
        ITransactionRepository repository,
        string status = "completed")
    {
        await repository.UpdateTransactionStatusAsync(transaction.Id, status);
        await repository.UpdateTransactionStatusAsync(withdrawalTransaction.Id, status);
        await repository.UpdateTransactionStatusAsync(depositTransaction.Id, status);
    }

    private static async Task HandleTransactionFailureAsync(
        Transaction transaction,
        Exception ex,
        ILogger<TransactionService> logger,
        ITransactionRepository repository,
        Counter errorsTotal)
    {
        logger.LogError(ex, "Error processing transaction");

        try
        {
            var failedTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId);
            if (failedTransaction != null)
            {
                await repository.UpdateTransactionStatusAsync(failedTransaction.Id, "failed");
            }
            else
            {
                logger.LogWarning("Could not find transaction to mark as failed");
            }
        }
        catch (Exception updateEx)
        {
            logger.LogError(updateEx, "Error updating transaction status to failed");
        }

        errorsTotal.WithLabels("CreateTransfer").Inc();
    }

    public async Task<TransactionResponse?> GetTransactionByTransferIdAsync(string transferId)
    {
        requestsTotal.WithLabels("GetTransactionByTransferId").Inc();
        try
        {
            var transaction = await repository.GetTransactionByTransferIdAsync(transferId);
            successesTotal.WithLabels("GetTransactionByTransferId").Inc();
            return transaction != null ? TransactionResponse.FromTransaction(transaction) : null;
        }
        catch (Exception ex)
        {
            errorsTotal.WithLabels("GetTransactionByTransferId").Inc();
            logger.LogError(ex, "Error retrieving transaction");
            throw;
        }
    }

    public async Task<IEnumerable<TransactionResponse>> GetTransactionsByAccountAsync(string accountId,
        int authenticatedUserId)
    {
        requestsTotal.WithLabels("GetTransactionsByAccount").Inc();
        try
        {
            // Validate accountId format
            if (!int.TryParse(accountId, out int accountIdInt))
            {
                logger.LogWarning("Invalid account ID format");
                errorsTotal.WithLabels("GetTransactionsByAccount").Inc();
                throw new ArgumentException("Account ID must be a valid integer.");
            }

            // Call UserAccountService to get account details
            logger.LogInformation("Fetching account details");
            var account = await userAccountClient.GetAccountAsync(accountIdInt);

            if (account == null)
            {
                logger.LogWarning("Account not found");
                errorsTotal.WithLabels("GetTransactionsByAccount").Inc();
                throw new InvalidOperationException("Account not found.");
            }

            // Validate that the authenticated user owns the account
            if (account.UserId != authenticatedUserId)
            {
                logger.LogWarning("User is not authorized to access transactions for account");
                errorsTotal.WithLabels("GetTransactionsByAccount").Inc();
                throw new UnauthorizedAccessException(
                    "You are not authorized to access transactions for this account.");
            }

            // Fetch transactions from the repository
            var transactions = await repository.GetTransactionsByAccountAsync(accountId);
            successesTotal.WithLabels("GetTransactionsByAccount").Inc();
            return transactions.Select(t => TransactionResponse.FromTransaction(t));
        }
        catch (Exception ex)
        {
            errorsTotal.WithLabels("GetTransactionsByAccount").Inc();
            logger.LogError(ex, "Error retrieving transactions");
            throw;
        }
    }

    private async Task<bool> CheckUserAccountServiceAvailabilityAsync()
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient("UserAccountClient"); // Fix: Use correct client name
            httpClient.Timeout = TimeSpan.FromSeconds(3);
            var response = await httpClient.GetAsync("/health"); // Make sure this matches your actual health endpoint
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Add this new method to queue a fraud check for later processing
    private void QueueFraudCheckForLaterProcessing(Transaction transaction)
    {
        try
        {
            var fraudMessage = new
            {
                transferId = transaction.TransferId,
                fromAccount = transaction.FromAccount,
                toAccount = transaction.ToAccount,
                amount = transaction.Amount,
                userId = transaction.UserId,
                timestamp = DateTime.UtcNow,
                isDelayed = true // Flag to indicate this is a delayed check
            };

            string messageJson = JsonConvert.SerializeObject(fraudMessage);
            
            logger.LogInformation("Queueing delayed fraud check: {Message}", messageJson);

            // Make sure the queue exists
            rabbitMqClient.DeclareQueue("CheckFraud", durable: true);
            
            // Queue the message - this will be processed when FraudDetection comes back online
            rabbitMqClient.Publish("CheckFraud", messageJson);
            
            logger.LogInformation("Successfully queued delayed fraud check for transaction {TransferId}", 
                transaction.TransferId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue delayed fraud check for transaction {TransferId}", 
                transaction.TransferId);
            // Don't rethrow - we still want the transaction to proceed
        }
    }

    // Add a new method to determine the transaction status
    private string DetermineTransactionStatus(bool fraudCheckPending, bool userAccountServiceAvailable)
    {
        if (fraudCheckPending) {
            // If fraud check is pending, the transaction should be pending
            return "pending";
        }
        
        if (!userAccountServiceAvailable) {
            // If user account service is down, the transaction is technically pending
            // but we'll still mark it as "processing" to indicate it's in progress
            return "processing";
        }
        
        // Everything is good, mark as completed
        return "completed";
    }
}