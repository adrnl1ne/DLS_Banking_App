using Microsoft.AspNetCore.Mvc;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service;

public interface IAccountService
{
    Task<List<AccountResponse>> GetAccountsAsync();
    Task<ActionResult<AccountResponse>> GetAccountAsync(int id);
    Task<ActionResult<AccountResponse>> CreateAccountAsync(AccountCreationRequest request);
    Task DeleteAccountAsync(int id);
    Task<ActionResult<AccountResponse>> RenameAccountAsync(int id, AccountRenameRequest request);
    Task<ActionResult<AccountResponse>> UpdateBalanceAsync(int id, AccountBalanceRequest request);
}
