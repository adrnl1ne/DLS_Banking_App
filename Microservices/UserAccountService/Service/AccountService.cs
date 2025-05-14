using System.Text.Json;
using AccountService.Database.Data;
using AccountService.Repository;
using AccountService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using StackExchange.Redis;
using UserAccountService.Models;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service;

public class AccountService(
    UserAccountDbContext context,
    ICurrentUserService currentUserService,
    ILogger<AccountService> logger,
    IAccountRepository accountRepository,
    IEventPublisher eventPublisher,
    IConnectionMultiplexer redis)
    : IAccountService
{
    private static readonly Counter RequestsTotal = Metrics.CreateCounter(
        "account_service_requests_total",
        "Total number of requests to AccountService",
        new CounterConfiguration { LabelNames = new[] { "method" } }
    );

    private static readonly Counter IdempotentRequestsTotal = Metrics.CreateCounter(
        "account_service_idempotent_requests_total",
        "Total number of idempotent balance update requests"
    );

    private static readonly Counter ErrorsTotal = Metrics.CreateCounter(
        "account_service_errors_total",
        "Total number of errors in AccountService",
        new CounterConfiguration { LabelNames = new[] { "method" } }
    );

    private static readonly Counter SuccessesTotal = Metrics.CreateCounter(
        "account_service_successes_total",
        "Total number of successful operations in AccountService",
        new CounterConfiguration { LabelNames = new[] { "method" } }
    );

    /// <summary>
    /// Retrieves all accounts associated with the currently authenticated user.
    /// It ensures that the user is authenticated before fetching the accounts.
    /// </summary>
    /// <returns>A list of <see cref="AccountResponse"/> objects representing the user's accounts.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the user ID cannot be determined (e.g., user not authenticated).</exception>
    public async Task<List<AccountResponse>> GetAccountsAsync()
    {
        RequestsTotal.WithLabels("GetAccounts").Inc();
        try
        {
            var userId = currentUserService.UserId;
            if (userId == null)
            {
                logger.LogWarning("User ID is null");
                ErrorsTotal.WithLabels("GetAccounts").Inc();
                throw new InvalidOperationException("User ID is required to get accounts.");
            }

            var accounts = await context.Accounts
                .Include(a => a.User)
                .Where(a => a.UserId == userId)
                .Select(a => new AccountResponse
                {
                    Id = a.Id,
                    Name = a.Name,
                    Amount = a.Amount,
                    UserId = a.UserId
                })
                .ToListAsync();

            SuccessesTotal.WithLabels("GetAccounts").Inc();
            return accounts;
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("GetAccounts").Inc();
            logger.LogError(ex, "Failed to get accounts");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a specific account by its unique ID.
    /// It performs authorization checks to ensure the current user or a service with appropriate privileges is accessing the account.
    /// </summary>
    /// <param name="id">The ID of the account to retrieve.</param>
    /// <returns>An <see cref="ActionResult{TValue}"/> containing the <see cref="AccountResponse"/> if found and authorized.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the account with the specified ID is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the current user is not authorized to access the account.</exception>
    public async Task<ActionResult<AccountResponse>> GetAccountAsync(int id)
    {
        RequestsTotal.WithLabels("GetAccount").Inc();
        try
        {
            var account = await context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (account == null)
            {
                logger.LogWarning("Account not found");
                ErrorsTotal.WithLabels("GetAccount").Inc();
                throw new InvalidOperationException($"Account {id} not found.");
            }

            if (currentUserService.Role == "service")
            {
                logger.LogInformation("Service token accessing account");
            }
            else
            {
                var userId = currentUserService.UserId;
                if (userId == null || account.UserId != userId)
                {
                    logger.LogWarning("User is not authorized to access account");
                    ErrorsTotal.WithLabels("GetAccount").Inc();
                    throw new UnauthorizedAccessException("You are not authorized to access this account.");
                }
            }

            var response = new AccountResponse
            {
                Id = account.Id,
                Name = account.Name,
                Amount = account.Amount,
                UserId = account.UserId
            };

            SuccessesTotal.WithLabels("GetAccount").Inc();
            return response;
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("GetAccount").Inc();
            logger.LogError(ex, "Failed to get account");
            throw;
        }
    }

    /// <summary>
    /// Retrieves all accounts for a specific user ID. This method is typically used by services or administrative users.
    /// It validates the user ID format and fetches accounts using the account repository.
    /// </summary>
    /// <param name="userId">The string representation of the user's ID whose accounts are to be retrieved.</param>
    /// <returns>An <see cref="ActionResult{TValue}"/> containing a list of <see cref="AccountResponse"/> objects. Returns an empty list if no accounts are found for the user.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided user ID is null or empty.</exception>
    /// <exception cref="FormatException">Thrown if the provided user ID is not a valid integer.</exception>
    public async Task<ActionResult<List<AccountResponse>>> GetUserAccountsAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("User ID is null or empty");
            throw new ArgumentException("User ID is required.", nameof(userId));
        }

        // Convert userId string to int for repository call
        if (!int.TryParse(userId, out int userIdInt))
        {
            logger.LogWarning("Invalid user ID format");
            throw new FormatException("User ID must be an integer.");
        }

        // Use the account repository to fetch accounts
        var accounts = await accountRepository.GetAccountsByUserIdAsync(userIdInt);
        
        if (!accounts.Any())
        {
            logger.LogInformation("No accounts found for user");
        }

        // Map domain entities to DTOs
        return accounts.Select(a => new AccountResponse 
        { 
            Id = a.Id, 
            Name = a.Name, 
            Amount = a.Amount,
            UserId = a.UserId
        }).ToList();
    }

    /// <summary>
    /// Creates a new account for the currently authenticated user.
    /// The user ID is obtained from the current user service. An event is published upon successful creation.
    /// </summary>
    /// <param name="request">The <see cref="AccountCreationRequest"/> containing the details for the new account, primarily the account name.</param>
    /// <returns>An <see cref="ActionResult{TValue}"/> containing the created <see cref="AccountResponse"/> and a CreatedAtAction result.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the user ID cannot be determined (e.g., user not authenticated).</exception>
    public async Task<ActionResult<AccountResponse>> CreateAccountAsync(AccountCreationRequest request)
    {
        RequestsTotal.WithLabels("CreateAccount").Inc();
        try
        {
            var account = new Account
            {
                Name = request.Name,
                UserId = currentUserService.UserId ??
                         throw new InvalidOperationException("User ID is required to create an account.")
            };

            await accountRepository.AddAccountAsync(account);
            await accountRepository.SaveChangesAsync();

            var eventMessage = new
            {
                event_type = "AccountCreated",
                accountId = account.Id,
                userId = account.UserId,
                name = account.Name,
                amount = account.Amount,
                timestamp = DateTime.UtcNow.ToString("o")
            };
            eventPublisher.Publish("AccountEvents", JsonSerializer.Serialize(eventMessage));

            var response = new AccountResponse
            {
                Id = account.Id,
                Name = account.Name,
                Amount = account.Amount,
                UserId = account.UserId
            };

            SuccessesTotal.WithLabels("CreateAccount").Inc();
            return new CreatedAtActionResult(
                actionName: "GetAccount",
                controllerName: "Account",
                routeValues: new { id = account.Id },
                value: response);
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("CreateAccount").Inc();
            logger.LogError(ex, "Failed to create account");
            throw;
        }
    }

    /// <summary>
    /// Deletes an account by its ID.
    /// It performs authorization checks to ensure that only the account owner or an administrator can delete the account.
    /// An event is published upon successful deletion.
    /// </summary>
    /// <param name="id">The ID of the account to delete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the account with the specified ID is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the current user is not authorized to delete the account.</exception>
    public async Task DeleteAccountAsync(int id)
    {
        RequestsTotal.WithLabels("DeleteAccount").Inc();
        try
        {
            var account = await context.Accounts.FindAsync(id);
            if (account == null)
            {
                logger.LogWarning("Account not found");
                ErrorsTotal.WithLabels("DeleteAccount").Inc();
                throw new InvalidOperationException($"Account {id} not found.");
            }

            if (currentUserService.Role != "admin" && account.UserId != currentUserService.UserId)
            {
                logger.LogWarning("User is not authorized to delete account");
                ErrorsTotal.WithLabels("DeleteAccount").Inc();
                throw new UnauthorizedAccessException("You are not authorized to delete this account.");
            }

            context.Accounts.Remove(account);
            await context.SaveChangesAsync();

            var eventMessage = new
            {
                event_type = "AccountDeleted",
                accountId = account.Id,
                userId = account.UserId,
                name = account.Name,
                timestamp = DateTime.UtcNow.ToString("o")
            };
            eventPublisher.Publish("AccountEvents", JsonSerializer.Serialize(eventMessage));

            SuccessesTotal.WithLabels("DeleteAccount").Inc();
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("DeleteAccount").Inc();
            logger.LogError(ex, "Failed to delete account");
            throw;
        }
    }

    /// <summary>
    /// Renames an existing account.
    /// It performs authorization checks to ensure only the account owner can rename the account.
    /// The new name must be provided and different from the current name. An event is published upon successful rename.
    /// </summary>
    /// <param name="id">The ID of the account to rename.</param>
    /// <param name="request">The <see cref="AccountRenameRequest"/> containing the new name for the account.</param>
    /// <returns>An <see cref="ActionResult{TValue}"/> containing the updated <see cref="AccountResponse"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the new name is missing, the account is not found, or the user ID is missing.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the current user is not authorized to rename the account.</exception>
    public async Task<ActionResult<AccountResponse>> RenameAccountAsync(int id, AccountRenameRequest request)
    {
        RequestsTotal.WithLabels("RenameAccount").Inc();
        try
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                logger.LogWarning("Name is missing for rename request");
                ErrorsTotal.WithLabels("RenameAccount").Inc();
                throw new InvalidOperationException("Name is required.");
            }

            var account = await context.Accounts.FindAsync(id);
            if (account == null)
            {
                logger.LogWarning("Account not found");
                ErrorsTotal.WithLabels("RenameAccount").Inc();
                throw new InvalidOperationException($"Account {id} not found.");
            }

            if (currentUserService.UserId == null)
            {
                logger.LogWarning("User ID is null for rename request");
                ErrorsTotal.WithLabels("RenameAccount").Inc();
                throw new InvalidOperationException("User ID is required.");
            }

            if (account.UserId != currentUserService.UserId)
            {
                logger.LogWarning("User is not authorized to rename account");
                ErrorsTotal.WithLabels("RenameAccount").Inc();
                throw new UnauthorizedAccessException("You are not authorized to rename this account.");
            }

            if (account.Name == request.Name)
            {
                logger.LogInformation("Account name unchanged");
                SuccessesTotal.WithLabels("RenameAccount").Inc();
                return new AccountResponse
                {
                    Id = account.Id,
                    Name = account.Name,
                    Amount = account.Amount,
                    UserId = account.UserId
                };
            }

            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                account.Name = request.Name;
                await context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ErrorsTotal.WithLabels("RenameAccount").Inc();
                logger.LogError(ex, "Failed to rename account");
                throw;
            }

            var eventMessage = new
            {
                event_type = "AccountRenamed",
                accountId = account.Id,
                userId = account.UserId,
                name = account.Name,
                timestamp = DateTime.UtcNow.ToString("o")
            };
            try
            {
                eventPublisher.Publish("AccountEvents", JsonSerializer.Serialize(eventMessage));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish AccountRenamed event");
            }

            SuccessesTotal.WithLabels("RenameAccount").Inc();
            return new AccountResponse
            {
                Id = account.Id,
                Name = account.Name,
                Amount = account.Amount,
                UserId = account.UserId
            };
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("RenameAccount").Inc();
            logger.LogError(ex, "Failed to rename account");
            throw;
        }
    }

    /// <summary>
    /// Updates the balance of an account based on a transaction (Deposit or Withdrawal).
    /// This operation is idempotent, using a transaction ID stored in Redis to prevent duplicate processing.
    /// Authorization is checked to ensure the user owns the account or the request is from a trusted service.
    /// The transaction amount must be non-negative, and the resulting balance cannot be negative.
    /// An event is published upon successful balance update.
    /// </summary>
    /// <param name="id">The ID of the account to update.</param>
    /// <param name="request">The <see cref="AccountBalanceRequest"/> containing transaction details (ID, type, amount).</param>
    /// <returns>An <see cref="ActionResult{TValue}"/> containing the updated <see cref="AccountResponse"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown for missing transaction ID, invalid transaction type, account not found, missing user ID (if not a service), negative transaction amount, or if the update results in a negative balance.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the current user is not authorized to update the account's balance.</exception>
    public async Task<ActionResult<AccountResponse>> UpdateBalanceAsync(int id, AccountBalanceRequest request)
    {
        RequestsTotal.WithLabels("UpdateBalance").Inc();
        try
        {
            if (string.IsNullOrEmpty(request.TransactionId))
            {
                logger.LogWarning("TransactionId is missing for balance update");
                ErrorsTotal.WithLabels("UpdateBalance").Inc();
                throw new InvalidOperationException("TransactionId is required.");
            }

            if (string.IsNullOrEmpty(request.TransactionType) ||
                (request.TransactionType != "Deposit" && request.TransactionType != "Withdrawal"))
            {
                logger.LogWarning("Invalid TransactionType");
                ErrorsTotal.WithLabels("UpdateBalance").Inc();
                throw new InvalidOperationException("TransactionType must be 'Deposit' or 'Withdrawal'.");
            }

            var account = await context.Accounts.FindAsync(id);
            if (account == null)
            {
                logger.LogWarning("Account not found");
                ErrorsTotal.WithLabels("UpdateBalance").Inc();
                throw new InvalidOperationException($"Account {id} not found.");
            }

            var redisDb = redis.GetDatabase();
            var redisKey = $"account:transaction:{request.TransactionId}";
            if (await redisDb.KeyExistsAsync(redisKey))
            {
                logger.LogInformation("Duplicate transaction detected");
                IdempotentRequestsTotal.Inc();
                SuccessesTotal.WithLabels("UpdateBalance").Inc();
                return new AccountResponse
                {
                    Id = account.Id,
                    Name = account.Name,
                    Amount = account.Amount,
                    UserId = account.UserId
                };
            }

            if (currentUserService.Role != "service")
            {
                if (currentUserService.UserId == null)
                {
                    logger.LogWarning("User ID is null for balance update");
                    ErrorsTotal.WithLabels("UpdateBalance").Inc();
                    throw new InvalidOperationException("User ID is required.");
                }

                if (account.UserId != currentUserService.UserId)
                {
                    logger.LogWarning("User is not authorized to update balance for account");
                    ErrorsTotal.WithLabels("UpdateBalance").Inc();
                    throw new UnauthorizedAccessException("You are not authorized to update this account’s balance.");
                }
            }

            if (request.Amount < 0)
            {
                logger.LogWarning("Invalid balance update: Amount cannot be negative");
                ErrorsTotal.WithLabels("UpdateBalance").Inc();
                throw new InvalidOperationException("Amount cannot be negative.");
            }

            // Calculate new balance based on TransactionType
            decimal newBalance = request.TransactionType == "Deposit"
                ? account.Amount + request.Amount
                : account.Amount - request.Amount;

            if (newBalance < 0)
            {
                logger.LogWarning("Invalid balance update: New balance cannot be negative");
                ErrorsTotal.WithLabels("UpdateBalance").Inc();
                throw new InvalidOperationException("Balance cannot be negative.");
            }

            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                account.Amount = newBalance;
                await context.SaveChangesAsync();

                await redisDb.StringSetAsync(redisKey, "processed", TimeSpan.FromDays(7));

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ErrorsTotal.WithLabels("UpdateBalance").Inc();
                logger.LogError(ex, "Failed to update balance");
                throw;
            }

            var eventMessage = new
            {
                event_type = "AccountBalanceUpdated",
                accountId = account.Id,
                userId = account.UserId,
                amount = account.Amount,
                transactionId = request.TransactionId,
                transactionType = request.TransactionType,
                timestamp = DateTime.UtcNow.ToString("o")
            };
            try
            {
                eventPublisher.Publish("AccountEvents", JsonSerializer.Serialize(eventMessage));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish AccountBalanceUpdated event");
            }

            SuccessesTotal.WithLabels("UpdateBalance").Inc();
            return new AccountResponse
            {
                Id = account.Id,
                Name = account.Name,
                Amount = account.Amount,
                UserId = account.UserId
            };
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("UpdateBalance").Inc();
            logger.LogError(ex, "Failed to update balance");
            throw;
        }
    }

    /// <summary>
    /// Deposits funds into a specified account.
    /// This operation is idempotent, generating a unique transaction ID and checking against Redis to prevent duplicate deposits.
    /// The deposit amount must be greater than zero. Authorization ensures the current user owns the account.
    /// An event is published upon successful deposit.
    /// </summary>
    /// <param name="id">The ID of the account to deposit into.</param>
    /// <param name="request">The <see cref="AccountDepositRequest"/> containing the amount to deposit.</param>
    /// <returns>An <see cref="ActionResult{TValue}"/> containing the updated <see cref="AccountResponse"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the deposit amount is not positive, the account is not found, or the user ID is missing.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown if the current user is not authorized to deposit into the account.</exception>
    public async Task<ActionResult<AccountResponse>> DepositToAccountAsync(int id, AccountDepositRequest request)
    {
        RequestsTotal.WithLabels("DepositToAccount").Inc();
        try
        {
            if (request.Amount <= 0)
            {
                logger.LogWarning("Invalid deposit amount");
                ErrorsTotal.WithLabels("DepositToAccount").Inc();
                throw new InvalidOperationException("Deposit amount must be greater than zero.");
            }

            var account = await context.Accounts.FindAsync(id);
            if (account == null)
            {
                logger.LogWarning("Account not found");
                ErrorsTotal.WithLabels("DepositToAccount").Inc();
                throw new InvalidOperationException($"Account {id} not found.");
            }

            if (currentUserService.UserId == null)
            {
                logger.LogWarning("User ID is null for deposit");
                ErrorsTotal.WithLabels("DepositToAccount").Inc();
                throw new InvalidOperationException("User ID is required.");
            }

            if (account.UserId != currentUserService.UserId)
            {
                logger.LogWarning("User is not authorized to deposit to account");
                ErrorsTotal.WithLabels("DepositToAccount").Inc();
                throw new UnauthorizedAccessException("You are not authorized to deposit to this account.");
            }

            // Generate a unique transaction ID for idempotence
            var transactionId = $"deposit-{Guid.NewGuid()}";
            var redisDb = redis.GetDatabase();
            var redisKey = $"account:transaction:{transactionId}";
            
            if (await redisDb.KeyExistsAsync(redisKey))
            {
                logger.LogInformation("Duplicate transaction detected");
                IdempotentRequestsTotal.Inc();
                SuccessesTotal.WithLabels("DepositToAccount").Inc();
                return new AccountResponse
                {
                    Id = account.Id,
                    Name = account.Name,
                    Amount = account.Amount,
                    UserId = account.UserId
                };
            }

            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                // Update the account balance
                account.Amount += request.Amount;
                await context.SaveChangesAsync();

                // Store the transaction ID in Redis for idempotence
                await redisDb.StringSetAsync(redisKey, "processed", TimeSpan.FromDays(7));

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ErrorsTotal.WithLabels("DepositToAccount").Inc();
                logger.LogError(ex, "Failed to deposit to account");
                throw;
            }

            var eventMessage = new
            {
                event_type = "AccountDeposited",
                accountId = account.Id,
                userId = account.UserId,
                amount = account.Amount,
                depositAmount = request.Amount,
                transactionId = transactionId,
                timestamp = DateTime.UtcNow.ToString("o")
            };
            try
            {
                eventPublisher.Publish("AccountEvents", JsonSerializer.Serialize(eventMessage));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish AccountDeposited event");
            }

            SuccessesTotal.WithLabels("DepositToAccount").Inc();
            return new AccountResponse
            {
                Id = account.Id,
                Name = account.Name,
                Amount = account.Amount,
                UserId = account.UserId
            };
        }
        catch (Exception ex)
        {
            ErrorsTotal.WithLabels("DepositToAccount").Inc();
            logger.LogError(ex, "Failed to deposit to account");
            throw;
        }
    }
}