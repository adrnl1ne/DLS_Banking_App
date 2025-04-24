using Microsoft.AspNetCore.Mvc;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service;

public interface IAccountService
{
    Task<ActionResult<AccountResponse>> GetAccountAsync(int id);
    Task<ActionResult<AccountResponse>> CreateAccountAsync(AccountCreationRequest request);
}
