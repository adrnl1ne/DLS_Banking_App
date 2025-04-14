using System.Security.Claims;
using System.Text.Json;
using AccountService.Models;
using AccountService.Repository;
using AccountService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AccountService.Service.AccountService;

public class AccountService(IAccountRepository accountRepository, IEventPublisher eventPublisher)
    : IAccountService
{
    public async Task<ActionResult<IEnumerable<Account>>> GetAccountsAsync(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = user.FindFirst("role")?.Value;

        Console.WriteLine($"Found userId claim: {userIdClaim}");
        Console.WriteLine("User Claims: " + string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}")));

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return new UnauthorizedObjectResult("User ID not found in token.");
        }

        if (!int.TryParse(userIdClaim, out int userId))
        {
            return new BadRequestObjectResult("Invalid user ID in token.");
        }

        if (roleClaim == "admin")
        {
            var accounts = await accountRepository.GetAllAccountsAsync();
            return new ActionResult<IEnumerable<Account>>(accounts);
        }

        var userAccounts = await accountRepository.GetAccountsByUserIdAsync(userId);
        return new ActionResult<IEnumerable<Account>>(userAccounts);
    }

    public async Task<ActionResult<Account>> GetAccountAsync(int id, ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = user.FindFirst("role")?.Value;

        Console.WriteLine($"Found userId claim: {userIdClaim}");
        Console.WriteLine("User Claims: " + string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}")));

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return new UnauthorizedObjectResult("User ID not found in token.");
        }

        if (!int.TryParse(userIdClaim, out int userId))
        {
            return new BadRequestObjectResult("Invalid user ID in token.");
        }

        var account = await accountRepository.GetAccountByIdAsync(id);
        if (account == null)
        {
            return new NotFoundResult();
        }

        if (roleClaim != "admin" && account.UserId != userId)
        {
            return new ForbidResult();
        }

        return account;
    }

    public async Task<ActionResult<Account>> CreateAccountAsync(Account account, ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        Console.WriteLine($"Found userId claim: {userIdClaim}");
        Console.WriteLine("User Claims: " + string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}")));

        if (string.IsNullOrEmpty(userIdClaim))
        {
            return new UnauthorizedObjectResult("User ID not found in token.");
        }

        if (!int.TryParse(userIdClaim, out int userId))
        {
            return new BadRequestObjectResult("Invalid user ID in token.");
        }

        account.UserId = userId;
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

        return new CreatedAtActionResult(nameof(GetAccountAsync), "Accounts", new { id = account.Id }, account);
    }
}
