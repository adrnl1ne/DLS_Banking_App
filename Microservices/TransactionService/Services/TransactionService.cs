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
    Histogram histogram)
    : ITransactionService
{
    public async Task<TransactionResponse> CreateTransferAsync(TransactionRequest request)
    {
        requestsTotal.WithLabels("CreateTransfer").Inc();
        try
        {
            logger.LogInformation("Creating transfer");

            // Perform health checks for both services upfront
            await CheckExternalServicesHealthAsync(logger, fraudDetectionService, validator, errorsTotal);

            // Validate the transfer request and fetch accounts
            var (fromAccount, toAccount) = await validator.ValidateTransferRequestAsync(request);

            // Create the pending transaction
            var transaction = await CreatePendingTransactionAsync(request, fromAccount, toAccount, logger, repository);

            try
            {
                // Perform fraud check
                var fraudResult = await fraudDetectionService.CheckFraudAsync(transaction.TransferId, transaction);

                // Check both IsFraud and Status to determine if the transaction should be declined
                if (fraudResult.IsFraud || fraudResult.Status == "declined")
                {
                    logger.LogWarning("Fraud detected for transaction {TransferId}: IsFraud={IsFraud}, Status={Status}",
                        transaction.TransferId, fraudResult.IsFraud, fraudResult.Status);
                    await repository.UpdateTransactionStatusAsync(transaction.Id, "declined");
                    errorsTotal.WithLabels("CreateTransfer").Inc();
                    throw new InvalidOperationException("Transaction declined due to potential fraud");
                }

                // Create child transactions (withdrawal and deposit)
                var (withdrawalTransaction, depositTransaction) = await CreateChildTransactionsAsync(
                    transaction, fromAccount, toAccount, repository);

                // Update account balances
                await UpdateAccountBalancesAsync(transaction, fromAccount, toAccount);

                // Update transaction statuses to completed
                await UpdateTransactionStatusesAsync(transaction, withdrawalTransaction, depositTransaction,
                    repository);

                logger.LogInformation("Transaction {TransferId} completed successfully", transaction.TransferId);

                // Track transaction amount in histogram for metrics
                histogram.WithLabels("transfer").Observe((double)transaction.Amount);

                var settings = new JsonSerializerSettings
                {
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc // Ensure UTC with 'Z' suffix
                };

                // Serialize the message
                rabbitMqClient.Publish("TransactionCreated", JsonConvert.SerializeObject(new
                {
                    transaction.TransferId,
                    transaction.Status,
                    transaction.Amount,
                    transaction.Description,
                    transaction.FromAccount,
                    transaction.ToAccount,
                    transaction.CreatedAt
                }, settings));
                
                successesTotal.WithLabels("CreateTransfer").Inc();
                return TransactionResponse.FromTransaction(transaction);
            }
            catch (Exception ex)
            {
                await HandleTransactionFailureAsync(transaction, ex, logger, repository, errorsTotal);
                throw;
            }
        }
        catch (Exception ex)
        {
            errorsTotal.WithLabels("CreateTransfer").Inc();
            logger.LogError(ex, "Error creating transfer");
            throw;
        }
    }

    private static async Task CheckExternalServicesHealthAsync(
        ILogger<TransactionService> logger,
        IFraudDetectionService fraudDetectionService,
        TransactionValidator validator,
        Counter errorsTotal)
    {
        // Only check fraud detection service - skip user account service check
        // We'll let messages queue up for UserAccountService
        
        // Check fraud detection service health - we still need this working
        if (!await fraudDetectionService.IsServiceAvailableAsync())
        {
            logger.LogWarning("Fraud detection service is down, rejecting transaction");
            errorsTotal.WithLabels("CreateTransfer").Inc();
            throw new ServiceUnavailableException(
                "FraudDetectionService",
                "The fraud detection service is currently unavailable. Please try again later.");
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
            TransactionType = request.TransactionType,
            Description = request.Description ?? $"Transfer from account {fromAccount.Id} to {toAccount.Id}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.CreateTransactionAsync(transaction);
        logger.LogInformation("Created pending transaction with TransferId: {TransferId}", transferId);
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

            // Serialize to string before publishing
            string fromAccountJson = System.Text.Json.JsonSerializer.Serialize(fromAccountMessage);
            string toAccountJson = System.Text.Json.JsonSerializer.Serialize(toAccountMessage);

            // Publish to RabbitMQ queue - this will work even if UserAccountService is down
            rabbitMqClient.Publish("AccountBalanceUpdates", fromAccountJson);
            rabbitMqClient.Publish("AccountBalanceUpdates", toAccountJson);
            
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
        ITransactionRepository repository)
    {
        await repository.UpdateTransactionStatusAsync(transaction.Id, "completed");
        await repository.UpdateTransactionStatusAsync(withdrawalTransaction.Id, "completed");
        await repository.UpdateTransactionStatusAsync(depositTransaction.Id, "completed");
    }

    private static async Task HandleTransactionFailureAsync(
        Transaction transaction,
        Exception ex,
        ILogger<TransactionService> logger,
        ITransactionRepository repository,
        Counter errorsTotal)
    {
        logger.LogError(ex, "Error processing transaction {TransferId}", transaction.TransferId);

        try
        {
            var failedTransaction = await repository.GetTransactionByTransferIdAsync(transaction.TransferId);
            if (failedTransaction != null)
            {
                await repository.UpdateTransactionStatusAsync(failedTransaction.Id, "failed");
            }
            else
            {
                logger.LogWarning("Could not find transaction {TransferId} to mark as failed", transaction.TransferId);
            }
        }
        catch (Exception updateEx)
        {
            logger.LogError(updateEx, "Error updating transaction {TransferId} status to failed",
                transaction.TransferId);
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
            logger.LogInformation("Fetching account from UserAccountService");
            var account = await userAccountClient.GetAccountAsync(accountIdInt);

            if (account == null)
            {
                logger.LogWarning("Account not found in UserAccountService");
                errorsTotal.WithLabels("GetTransactionsByAccount").Inc();
                throw new InvalidOperationException($"Account {accountId} not found.");
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
            return transactions.Select(TransactionResponse.FromTransaction);
        }
        catch (Exception ex)
        {
            errorsTotal.WithLabels("GetTransactionsByAccount").Inc();
            logger.LogError(ex, "Error retrieving transactions for account {AccountId}", accountId);
            throw;
        }
    }
}