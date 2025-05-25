using Newtonsoft.Json;
using Polly;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Models;
using TransactionService.Services.Interface;
using Prometheus;
using TransactionService.Exceptions;
using TransactionService.Infrastructure.Messaging;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Infrastructure.Redis;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    IRabbitMqClient rabbitMqClient,  // Changed back to IRabbitMqClient
    Histogram histogram,
    IRedisClient redisClient,
    IHttpClientFactory httpClientFactory)
    : ITransactionService
{
    // Suspicious transaction thresholds
    private readonly decimal _highRiskAmountThreshold = 1000; // Transactions >= 1000 are considered high risk

    public async Task<TransactionResponse> CreateTransferAsync(TransactionRequest request)
    {
        requestsTotal.WithLabels("CreateTransfer").Inc();
        try
        {
            logger.LogInformation("Creating transfer");

            // Check service availability
            bool isUserAccountServiceAvailable = await CheckUserAccountServiceAvailabilityAsync();
            bool isFraudDetectionAvailable = await fraudDetectionService.IsServiceAvailableAsync();
            
            // Check for suspicious amounts early
            bool isSuspiciousAmount = IsSuspiciousAmount(request.Amount);
            if (isSuspiciousAmount)
            {
                logger.LogWarning("Suspicious transaction amount detected");
            }

            // Validate the transfer request and fetch accounts
            var (fromAccount, toAccount) = await validator.ValidateTransferRequestAsync(request);

            // Create the pending transaction
            var transaction = await CreatePendingTransactionAsync(request, fromAccount, toAccount, logger, repository);

            // Set initial fraud check status
            bool fraudCheckPending = false;
            bool fraudDetected = false;

            try
            {
                // Always send fraud check request and wait for result
                if (isFraudDetectionAvailable)
                {
                    try
                    {
                        // Send fraud check request - this now always returns immediately
                        // The actual fraud result will come through FraudResultConsumer
                        await fraudDetectionService.CheckFraudAsync(transaction.TransferId, transaction);
                        
                        // Mark transaction as pending fraud verification - NO balance updates yet
                        transaction.FraudCheckResult = "pending_verification";
                        fraudCheckPending = true;
                        await repository.UpdateTransactionAsync(transaction);
                        
                        logger.LogInformation("Fraud check request sent for {TransferId}, waiting for result", transaction.TransferId);
                    }
                    catch (ServiceUnavailableException)
                    {
                        // Handle as if fraud detection is down
                        logger.LogWarning("Fraud detection service unavailable, queueing request and flagging transaction for review");
                        QueueFraudCheckForLaterProcessing(transaction);
                        transaction.FraudCheckResult = "pending_review";
                        fraudCheckPending = true;
                        await repository.UpdateTransactionAsync(transaction);
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
                logger.LogWarning(ex, "Fraud detection service unavailable, proceeding with transaction and flagging for review");

                // Add a flag to the transaction for later review
                transaction.FraudCheckResult = "pending_review";
                fraudCheckPending = true;
                await repository.UpdateTransactionAsync(transaction);
            }

            // Block high-value transactions immediately if they meet our risk criteria
            // This is a direct application-level check, not dependent on external fraud service
            if (isSuspiciousAmount && !fraudDetected)
            {
                // For amounts over threshold, we need additional verification
                logger.LogWarning("High-value transaction requires additional verification");
                
                // Mark the transaction for manual review if it wasn't already declined
                if (!fraudDetected)
                {
                    fraudCheckPending = true;
                    transaction.FraudCheckResult = "pending_review";
                    await repository.UpdateTransactionAsync(transaction);
                    
                    // Queue an additional, higher-priority fraud check
                    QueuePriorityFraudCheck(transaction);
                }
            }

            // Create child transactions (withdrawal and deposit)
            var (withdrawalTransaction, depositTransaction) = await CreateChildTransactionsAsync(
                transaction, fromAccount, toAccount, repository);

            // Initialize balance update tracking in Redis
            await InitializeBalanceUpdateTrackingAsync(transaction.TransferId);

            // IMPORTANT: NEVER queue balance updates immediately - ALWAYS wait for fraud verification
            // All transactions must be verified before any balance updates occur
            logger.LogInformation("Transaction created and pending fraud verification - NO balance updates will occur until fraud check completes");
            
            // Always set initial status to pending (waiting for fraud verification)
            string transactionStatus = "pending";
            await UpdateTransactionStatusesAsync(transaction, withdrawalTransaction, depositTransaction,
                repository, transactionStatus);

            logger.LogInformation("Transaction created with status: pending (awaiting fraud verification)");

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
                    Status = "processing", // Always start as processing
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

            // Update status message to reflect that ALL transactions wait for fraud verification
            string statusMessage = "Transaction created and awaiting fraud verification. Balance updates will occur after verification completes.";

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
            Description = request.Description ?? $"Transfer",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            TransactionType = "Withdrawal",
        };

        await repository.CreateTransactionAsync(transaction);
        logger.LogInformation("Created pending transaction");
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
            Description = $"Withdrawal",
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
            Description = $"Deposit",
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
        logger.LogInformation("Queueing balance updates");

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
            
            logger.LogInformation("🔗 Ensuring RabbitMQ connection and queue declaration");
            try 
            {
                rabbitMqClient.DeclareQueue(queueName, durable: true);
                logger.LogInformation("✅ Queue ready for publishing");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "⚠️ Error declaring queue, will try to publish anyway");
            }

            // Serialize to string before publishing
            string fromAccountJson = System.Text.Json.JsonSerializer.Serialize(fromAccountMessage);
            string toAccountJson = System.Text.Json.JsonSerializer.Serialize(toAccountMessage);

            // Publish withdrawal message
            logger.LogInformation("📤 PUBLISHING WITHDRAWAL MESSAGE");
            
            rabbitMqClient.Publish(queueName, fromAccountJson);
            logger.LogInformation("✅ WITHDRAWAL MESSAGE PUBLISHED SUCCESSFULLY");
            
            // Add a small delay to ensure messages are processed in order
            await Task.Delay(100);
            
            // Publish deposit message
            logger.LogInformation("📤 PUBLISHING DEPOSIT MESSAGE");
            
            rabbitMqClient.Publish(queueName, toAccountJson);
            logger.LogInformation("✅ DEPOSIT MESSAGE PUBLISHED SUCCESSFULLY");
            
            logger.LogInformation("🎉 BOTH WITHDRAWAL AND DEPOSIT MESSAGES PUBLISHED SUCCESSFULLY");
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ FAILED to queue balance updates");
            throw;
        }
    }

    // Add new method to initialize balance update tracking
    private async Task InitializeBalanceUpdateTrackingAsync(string transferId)
    {
        try
        {
            var trackingKey = $"transaction:tracking:{transferId}";
            
            var trackingData = new
            {
                withdrawalCompleted = false,
                depositCompleted = false,
                createdAt = DateTime.UtcNow.ToString("o")
            };
            
            await redisClient.SetAsync(trackingKey, JsonConvert.SerializeObject(trackingData), TimeSpan.FromHours(24));
            logger.LogInformation("Initialized balance update tracking for transaction {TransferId}", transferId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize balance update tracking for transaction {TransferId}", transferId);
            // Don't throw - this is not critical for transaction processing
        }
    }

    // Add new method to handle balance update confirmations
    public async Task HandleBalanceUpdateConfirmationAsync(string transferId, string transactionType, bool success)
    {
        try
        {
            logger.LogInformation("🔄 HANDLING BALANCE UPDATE CONFIRMATION: TransferId={TransferId}, Type={Type}, Success={Success}",
                transferId, transactionType, success);

            // The tracking key should match what was set during initialization
            var trackingKey = $"transaction:tracking:{transferId}";
            
            var trackingDataJson = await redisClient.GetAsync(trackingKey);
            if (string.IsNullOrEmpty(trackingDataJson))
            {
                logger.LogWarning("❌ No tracking data found for transaction {TransferId}. Looking for any Redis keys with this transferId...", transferId);
                
                // Debug: Try to find any keys related to this transferId
                try
                {
                    // This is for debugging - in production you might want to remove this
                    logger.LogInformation("🔍 SEARCHING for Redis keys containing transferId {TransferId}", transferId);
                    // For now, let's try to initialize tracking if it doesn't exist
                    await InitializeBalanceUpdateTrackingAsync(transferId);
                    trackingDataJson = await redisClient.GetAsync(trackingKey);
                }
                catch (Exception debugEx)
                {
                    logger.LogError(debugEx, "Error during Redis key search");
                }
                
                if (string.IsNullOrEmpty(trackingDataJson))
                {
                    logger.LogWarning("❌ Still no tracking data found for transaction {TransferId}, creating new tracking", transferId);
                    await InitializeBalanceUpdateTrackingAsync(transferId);
                    trackingDataJson = await redisClient.GetAsync(trackingKey);
                }
            }
            
            logger.LogInformation("📊 Found tracking data: {TrackingData}", trackingDataJson);
            
            dynamic trackingData = JsonConvert.DeserializeObject(trackingDataJson) ?? new { withdrawalCompleted = false, depositCompleted = false };
            
            // Update the appropriate field based on transaction type
            if (transactionType.Equals("Withdrawal", StringComparison.OrdinalIgnoreCase))
            {
                trackingData.withdrawalCompleted = success;
                logger.LogInformation("✅ UPDATED withdrawal status to {Success}", success);
            }
            else if (transactionType.Equals("Deposit", StringComparison.OrdinalIgnoreCase))
            {
                trackingData.depositCompleted = success;
                logger.LogInformation("✅ UPDATED deposit status to {Success}", success);
            }
            else
            {
                logger.LogWarning("⚠️ UNKNOWN transaction type: {TransactionType}", transactionType);
                return; // Don't process unknown transaction types
            }
            
            // Save updated tracking data
            var updatedTrackingJson = JsonConvert.SerializeObject(trackingData);
            await redisClient.SetAsync(trackingKey, updatedTrackingJson, TimeSpan.FromHours(24));

            // Check if both updates are completed
            bool withdrawalDone = trackingData.withdrawalCompleted == true;
            bool depositDone = trackingData.depositCompleted == true;
            
            logger.LogInformation("🔍 COMPLETION CHECK: Withdrawal={WithdrawalDone}, Deposit={DepositDone}", withdrawalDone, depositDone);
            
            if (withdrawalDone && depositDone)
            {
                logger.LogInformation("🎉 BOTH BALANCE UPDATES COMPLETED, marking as completed");
                
                // Update all related transactions to completed
                var transaction = await repository.GetTransactionByTransferIdAsync(transferId);
                if (transaction != null)
                {
                    var withdrawalTransaction = await repository.GetTransactionByTransferIdAsync(transferId + "-withdrawal");
                    var depositTransaction = await repository.GetTransactionByTransferIdAsync(transferId + "-deposit");
                    
                    if (withdrawalTransaction != null && depositTransaction != null)
                    {
                        await UpdateTransactionStatusesAsync(transaction, withdrawalTransaction, depositTransaction, repository, "completed");
                        logger.LogInformation("✅ MARKED ALL TRANSACTIONS AS COMPLETED");
                    }
                    
                    // Publish updated transaction event
                    var settings = new JsonSerializerSettings
                    {
                        DateFormatHandling = DateFormatHandling.IsoDateFormat,
                        DateTimeZoneHandling = DateTimeZoneHandling.Utc
                    };

                    var messageJson = JsonConvert.SerializeObject(new
                    {
                        transaction.TransferId,
                        Status = "completed",
                        transaction.Amount,
                        transaction.Description,
                        transaction.FromAccount,
                        transaction.ToAccount,
                        transaction.CreatedAt,
                        CompletedAt = DateTime.UtcNow
                    }, settings);

                    rabbitMqClient.Publish("TransactionCompleted", messageJson);
                    logger.LogInformation("📢 PUBLISHED transaction completed event");
                }
            }
            else if (success == false)
            {
                logger.LogError("❌ BALANCE UPDATE FAILED for transaction type {TransactionType}", transactionType);
                
                // Mark transaction as failed if any balance update fails
                var transaction = await repository.GetTransactionByTransferIdAsync(transferId);
                if (transaction != null)
                {
                    await repository.UpdateTransactionStatusAsync(transaction.Id, "failed");
                    logger.LogInformation("💥 MARKED transaction as failed due to balance update failure");
                }
            }
            else
            {
                logger.LogInformation("⏳ WAITING for more confirmations. Current status: Withdrawal={WithdrawalDone}, Deposit={DepositDone}", 
                    withdrawalDone, depositDone);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "💥 ERROR handling balance update confirmation for {TransferId}", transferId);
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
            logger.LogInformation("Fetching account details");
            var account = await userAccountClient.GetAccountAsync(accountIdInt);

            if (account == null)
            {
                logger.LogWarning("Account not found");
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
            using var httpClient = httpClientFactory.CreateClient("UserAccountService");
            httpClient.Timeout = TimeSpan.FromSeconds(3);
            var response = await httpClient.GetAsync("/health");
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
            
            logger.LogInformation("QUEUEING DELAYED FRAUD CHECK MESSAGE: {Message}", messageJson);

            // Make sure the queue exists
            rabbitMqClient.DeclareQueue("CheckFraud", durable: true);
            
            // Queue the message - this will be processed when FraudDetection comes back online
            rabbitMqClient.Publish("CheckFraud", messageJson);
            
            logger.LogInformation("SUCCESS: Queued delayed fraud check for transaction {TransferId} with isDelayed=true", 
                transaction.TransferId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue delayed fraud check for transaction {TransferId}", 
                transaction.TransferId);
            // Don't rethrow - we still want the transaction to proceed
        }
    }

    // Queue a high-priority fraud check for suspicious transactions
    private void QueuePriorityFraudCheck(Transaction transaction)
    {
        try
        {
            var priorityFraudMessage = new
            {
                transferId = transaction.TransferId,
                fromAccount = transaction.FromAccount,
                toAccount = transaction.ToAccount,
                amount = transaction.Amount,
                userId = transaction.UserId,
                timestamp = DateTime.UtcNow,
                priority = "high",
                suspiciousAmount = true
            };

            string messageJson = JsonConvert.SerializeObject(priorityFraudMessage);
            
            logger.LogInformation("Queueing high-priority fraud check for suspicious amount");

            // Make sure the queue exists
            rabbitMqClient.DeclareQueue("PriorityFraudCheck", durable: true);
            
            // Queue the high-priority message
            rabbitMqClient.Publish("PriorityFraudCheck", messageJson);
            
            logger.LogInformation("Successfully queued priority fraud check");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue priority fraud check");
            // Don't rethrow - we still want the transaction to proceed to pending state
        }
    }

    // Helper method to determine if an amount is suspicious
    private bool IsSuspiciousAmount(decimal amount)
    {
        // Current implementation: consider amounts >= 1000 as suspicious
        return amount >= _highRiskAmountThreshold;
    }
    
    // Add a new method to determine the transaction status
    private string DetermineTransactionStatus(bool fraudCheckPending, bool userAccountServiceAvailable, bool isSuspiciousAmount)
    {
        if (fraudCheckPending || isSuspiciousAmount) {
            // If fraud check is pending or amount is suspicious, the transaction should be pending
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

    public async Task ProcessFraudResultAsync(FraudResult fraudResult)
    {
        try
        {
            logger.LogInformation("Processing fraud result for {TransferId}: IsFraud={IsFraud}, Status={Status}", 
                fraudResult.TransferId, fraudResult.IsFraud, fraudResult.Status);

            var transaction = await repository.GetTransactionByTransferIdAsync(fraudResult.TransferId);
            if (transaction == null)
            {
                logger.LogWarning("Transaction not found for fraud result: {TransferId}", fraudResult.TransferId);
                return;
            }

            // Get child transactions
            var withdrawalTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-withdrawal");
            var depositTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId + "-deposit");

            if (fraudResult.IsFraud || fraudResult.Status == "declined")
            {
                // FRAUD DETECTED - REJECT THE TRANSACTION
                logger.LogWarning("Fraud detected for transaction {TransferId}, rejecting transaction", transaction.TransferId);
                
                // Update fraud check result
                transaction.FraudCheckResult = "fraud_detected";
                await repository.UpdateTransactionAsync(transaction);

                // Mark all transactions as declined/rejected
                await repository.UpdateTransactionStatusAsync(transaction.Id, "declined");
                
                if (withdrawalTransaction != null)
                {
                    await repository.UpdateTransactionStatusAsync(withdrawalTransaction.Id, "declined");
                }
                
                if (depositTransaction != null)
                {
                    await repository.UpdateTransactionStatusAsync(depositTransaction.Id, "declined");
                }

                logger.LogInformation("Transaction {TransferId} and all child transactions marked as declined due to fraud detection", transaction.TransferId);
                
                // Publish transaction declined event
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        DateFormatHandling = DateFormatHandling.IsoDateFormat,
                        DateTimeZoneHandling = DateTimeZoneHandling.Utc
                    };

                    var declinedMessageJson = JsonConvert.SerializeObject(new
                    {
                        transaction.TransferId,
                        Status = "declined",
                        Reason = "fraud_detected",
                        transaction.Amount,
                        transaction.Description,
                        transaction.FromAccount,
                        transaction.ToAccount,
                        transaction.CreatedAt,
                        DeclinedAt = DateTime.UtcNow
                    }, settings);

                    rabbitMqClient.Publish("TransactionDeclined", declinedMessageJson);
                    logger.LogInformation("Published transaction declined event for {TransferId}", transaction.TransferId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to publish transaction declined event for {TransferId}", transaction.TransferId);
                }
            }
            else
            {
                // FRAUD CHECK PASSED - PROCEED WITH BALANCE UPDATES
                logger.LogInformation("Fraud check passed for {TransferId}, proceeding with balance updates", transaction.TransferId);
                
                // Update fraud check result
                transaction.FraudCheckResult = "verified";
                await repository.UpdateTransactionAsync(transaction);
                
                // Parse account information
                if (int.TryParse(transaction.FromAccount, out var fromAccountId) && 
                    int.TryParse(transaction.ToAccount, out var toAccountId))
                {
                    var fromAccount = new Account { Id = fromAccountId, UserId = transaction.UserId };
                    var toAccount = new Account { Id = toAccountId };
                    
                    // NOW queue balance updates since fraud check passed
                    await UpdateAccountBalancesAsync(transaction, fromAccount, toAccount);
                    
                    // Update transaction status to processing (waiting for balance updates)
                    await repository.UpdateTransactionStatusAsync(transaction.Id, "processing");
                    
                    if (withdrawalTransaction != null)
                    {
                        await repository.UpdateTransactionStatusAsync(withdrawalTransaction.Id, "processing");
                    }
                    
                    if (depositTransaction != null)
                    {
                        await repository.UpdateTransactionStatusAsync(depositTransaction.Id, "processing");
                    }
                    
                    logger.LogInformation("Balance updates queued for transaction {TransferId}, status updated to processing", transaction.TransferId);
                }
                else
                {
                    logger.LogError("Invalid account IDs for transaction {TransferId}: From={FromAccount}, To={ToAccount}", 
                        transaction.TransferId, transaction.FromAccount, transaction.ToAccount);
                        
                    // Mark as failed due to invalid account data
                    await repository.UpdateTransactionStatusAsync(transaction.Id, "failed");
                    if (withdrawalTransaction != null)
                    {
                        await repository.UpdateTransactionStatusAsync(withdrawalTransaction.Id, "failed");
                    }
                    if (depositTransaction != null)
                    {
                        await repository.UpdateTransactionStatusAsync(depositTransaction.Id, "failed");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing fraud result for {TransferId}", fraudResult.TransferId);
        }
    }
}