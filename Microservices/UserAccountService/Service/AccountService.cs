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
