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
                logger.LogWarning("Account {AccountId} not found", id);
                ErrorsTotal.WithLabels("GetAccount").Inc();
                throw new InvalidOperationException($"Account {id} not found.");
            }

            if (currentUserService.Role == "service")
            {
                logger.LogInformation("Service token accessing account {AccountId}", id);
            }
            else
            {
                var userId = currentUserService.UserId;
                if (userId == null || account.UserId != userId)
                {
                    logger.LogWarning("User {UserId} is not authorized to access account {AccountId}", userId, id);
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
            logger.LogError(ex, "Failed to get account {AccountId}", id);
            throw;
        }
    }

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

    public async Task DeleteAccountAsync(int id)
    {
        RequestsTotal.WithLabels("DeleteAccount").Inc();
        try
        {
            var account = await context.Accounts.FindAsync(id);
            if (account == null)
            {
                logger.LogWarning("Account {AccountId} not found", id);
                ErrorsTotal.WithLabels("DeleteAccount").Inc();
                throw new InvalidOperationException($"Account {id} not found.");
            }

            if (currentUserService.Role != "admin" && account.UserId != currentUserService.UserId)
            {
                logger.LogWarning("User {UserId} is not authorized to delete account {AccountId}",
                    currentUserService.UserId, id);
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
            logger.LogError(ex, "Failed to delete account {AccountId}", id);
            throw;
        }
    }

    public async Task<ActionResult<AccountResponse>> RenameAccountAsync(int id, AccountRenameRequest request)
    {
        RequestsTotal.WithLabels("RenameAccount").Inc();
        try
        {
            if (string.IsNullOrEmpty(request.Name))
            {
                logger.LogWarning("Name is missing for rename request on account {AccountId}", id);
                ErrorsTotal.WithLabels("RenameAccount").Inc();
                throw new InvalidOperationException("Name is required.");
            }

            var account = await context.Accounts.FindAsync(id);
            if (account == null)
            {
                logger.LogWarning("Account {AccountId} not found", id);
                ErrorsTotal.WithLabels("RenameAccount").Inc();
                throw new InvalidOperationException($"Account {id} not found.");
            }

            if (currentUserService.UserId == null)
            {
                logger.LogWarning("User ID is null for rename request on account {AccountId}", id);
                ErrorsTotal.WithLabels("RenameAccount").Inc();
                throw new InvalidOperationException("User ID is required.");
            }

            if (account.UserId != currentUserService.UserId)
            {
                logger.LogWarning("User {UserId} is not authorized to rename account {AccountId}",
                    currentUserService.UserId, id);
                ErrorsTotal.WithLabels("RenameAccount").Inc();
                throw new UnauthorizedAccessException("You are not authorized to rename this account.");
            }

            if (account.Name == request.Name)
            {
                logger.LogInformation("Account {AccountId} name unchanged", id);
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
                logger.LogError(ex, "Failed to rename account {AccountId}", id);
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
                logger.LogError(ex, "Failed to publish AccountRenamed event for account {AccountId}", id);
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
            logger.LogError(ex, "Failed to rename account {AccountId}", id);
            throw;
        }
    }

    public async Task<ActionResult<AccountResponse>> UpdateBalanceAsync(int id, AccountBalanceRequest request)
    {
        RequestsTotal.WithLabels("UpdateBalance").Inc();
        try
        {
            if (string.IsNullOrEmpty(request.TransactionId))
            {
                logger.LogWarning("TransactionId is missing for balance update on account {AccountId}", id);
                ErrorsTotal.WithLabels("UpdateBalance").Inc();
                throw new InvalidOperationException("TransactionId is required.");
            }

            if (string.IsNullOrEmpty(request.TransactionType) ||
                (request.TransactionType != "Deposit" && request.TransactionType != "Withdrawal"))
            {
                logger.LogWarning("Invalid TransactionType {TransactionType} for balance update on account {AccountId}",
                    request.TransactionType, id);
                ErrorsTotal.WithLabels("UpdateBalance").Inc();
                throw new InvalidOperationException("TransactionType must be 'Deposit' or 'Withdrawal'.");
            }

            var account = await context.Accounts.FindAsync(id);
            if (account == null)
            {
                logger.LogWarning("Account {AccountId} not found", id);
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
                    logger.LogWarning("User ID is null for balance update on account {AccountId}", id);
                    ErrorsTotal.WithLabels("UpdateBalance").Inc();
                    throw new InvalidOperationException("User ID is required.");
                }

                if (account.UserId != currentUserService.UserId)
                {
                    logger.LogWarning("User {UserId} is not authorized to update balance for account {AccountId}",
                        currentUserService.UserId, id);
                    ErrorsTotal.WithLabels("UpdateBalance").Inc();
                    throw new UnauthorizedAccessException("You are not authorized to update this account’s balance.");
                }
            }

            if (request.Amount < 0)
            {
                logger.LogWarning("Invalid balance update: Amount {Amount} cannot be negative for account {AccountId}",
                    request.Amount, id);
                ErrorsTotal.WithLabels("UpdateBalance").Inc();
                throw new InvalidOperationException("Amount cannot be negative.");
            }

            // Calculate new balance based on TransactionType
            decimal newBalance = request.TransactionType == "Deposit"
                ? account.Amount + request.Amount
                : account.Amount - request.Amount;

            if (newBalance < 0)
            {
                logger.LogWarning(
                    "Invalid balance update: New balance {NewBalance} cannot be negative for account {AccountId}",
                    newBalance, id);
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
                logger.LogError(ex, "Failed to update balance for account {AccountId}", id);
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
                logger.LogError(ex, "Failed to publish AccountBalanceUpdated event for account {AccountId}", id);
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
            logger.LogError(ex, "Failed to update balance for account {AccountId}", id);
            throw;
        }
    }
}