using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using UserAccountService.Models;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service;

public interface IAccountService
{
    Task<List<AccountResponse>> GetAccountsAsync();
    Task<ActionResult<AccountResponse>> GetAccountAsync(int id);
    Task<ActionResult<List<AccountResponse>>> GetUserAccountsAsync(string userId);
    Task<ActionResult<AccountResponse>> CreateAccountAsync(AccountCreationRequest request);
    Task DeleteAccountAsync(int id);
    Task<ActionResult<AccountResponse>> RenameAccountAsync(int id, AccountRenameRequest request);
    Task<ActionResult<AccountResponse>> UpdateBalanceAsync(int id, AccountBalanceRequest request);
    Task<ActionResult<AccountResponse>> DepositToAccountAsync(int id, AccountDepositRequest request);
    Task<ApiResponse<Account>> UpdateBalanceAsSystemAsync(int accountId, AccountBalanceRequest request);
}
