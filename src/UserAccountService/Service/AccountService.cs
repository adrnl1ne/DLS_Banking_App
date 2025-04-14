using System.Text.Json;
using AccountService.Models;
using AccountService.Repository;
using AccountService.Services;
using Microsoft.AspNetCore.Mvc;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service;

public class AccountService(
    IAccountRepository accountRepository,
    IEventPublisher eventPublisher,
    ICurrentUserService currentUserService)
    : IAccountService
{
    public async Task<ActionResult<IEnumerable<AccountResponse>>> GetAccountsAsync()
    {
        IEnumerable<Account> accounts;
        if (string.Equals(currentUserService.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            accounts = await accountRepository.GetAllAccountsAsync();
        }
        else
        {
            accounts = await accountRepository.GetAccountsByUserIdAsync(currentUserService.UserId);
        }

        var response = accounts.Select(a => new AccountResponse
        {
            Name = a.Name,
            Amount = a.Amount
        });

        return new ActionResult<IEnumerable<AccountResponse>>(response);
    }

    public async Task<ActionResult<AccountResponse>> GetAccountAsync(int id)
    {
        var account = await accountRepository.GetAccountByIdAsync(id);
        if (account == null)
        {
            return new NotFoundResult();
        }

        if (!string.Equals(currentUserService.Role, "admin", StringComparison.OrdinalIgnoreCase) &&
            account.UserId != currentUserService.UserId)
        {
            return new ForbidResult();
        }

        var response = new AccountResponse
        {
            Name = account.Name,
            Amount = account.Amount
        };

        return response;
    }

    public async Task<ActionResult<AccountResponse>> CreateAccountAsync(AccountCreationRequest request)
    {
        var account = new Account
        {
            Name = request.Name,
            UserId = currentUserService.UserId
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
