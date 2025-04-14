using System.Security.Claims;
using AccountService.Models;
using Microsoft.AspNetCore.Mvc;

namespace AccountService.Service.AccountService;

public interface IAccountService
{
    Task<ActionResult<IEnumerable<Account>>> GetAccountsAsync(ClaimsPrincipal user);
    Task<ActionResult<Account>> GetAccountAsync(int id, ClaimsPrincipal user);
    Task<ActionResult<Account>> CreateAccountAsync(Account account, ClaimsPrincipal user);
}
