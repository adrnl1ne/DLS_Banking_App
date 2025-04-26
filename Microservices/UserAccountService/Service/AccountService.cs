using System.Text.Json;
using AccountService.Database.Data;
using AccountService.Repository;
using AccountService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserAccountService.Models;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service;

public class AccountService(
    UserAccountDbContext context,
    ICurrentUserService currentUserService,
    ILogger<AccountService> logger,
    IAccountRepository accountRepository,
    IEventPublisher eventPublisher)
    : IAccountService
{

    // Changed to IEventPublisher

    public async Task<ActionResult<AccountResponse>> GetAccountAsync(int id)
    {
        var account = await context.Accounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (account == null)
        {
            logger.LogWarning("Account {AccountId} not found", id);
            throw new InvalidOperationException($"Account {id} not found.");
        }

        // Check if the token is a service token
        if (currentUserService.Role == "service")
        {
            logger.LogInformation("Service token accessing account {AccountId}", id);
        }
        else
        {
            // For user tokens, validate ownership
            var userId = currentUserService.UserId;
            if (userId == null || account.UserId != userId)
            {
                logger.LogWarning("User {UserId} is not authorized to access account {AccountId}", userId, id);
                throw new UnauthorizedAccessException("You are not authorized to access this account.");
            }
        }

        return new AccountResponse
        {
            Id = account.Id,
            Name = account.Name,
            Amount = account.Amount,
            UserId = account.UserId
        };
    }

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
            logger.LogWarning("Invalid user ID format: {UserId}", userId);
            throw new FormatException("User ID must be an integer.");
        }

        // Use the account repository to fetch accounts
        var accounts = await accountRepository.GetAccountsByUserIdAsync(userIdInt);
        
        if (!accounts.Any())
        {
            logger.LogInformation("No accounts found for user {UserId}", userId);
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

    public async Task<ActionResult<AccountResponse>> CreateAccountAsync(AccountCreationRequest request)
    {
        var account = new Account
        {
            Name = request.Name,
            UserId = currentUserService.UserId ?? throw new InvalidOperationException("User ID is required to create an account."),
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
            Name = account.Name,
            Amount = account.Amount
        };

        return new CreatedAtActionResult(
            actionName: "GetAccount",
            controllerName: "Account",
            routeValues: new { id = account.Id },
            value: response);
    }
}
