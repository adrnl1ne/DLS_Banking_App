using TransactionService.Models;

namespace TransactionService.Services.Interface;

public interface IUserAccountClient
{
    Task<Account?> GetAccountAsync(int id);
    Task UpdateBalanceAsync(int accountId, AccountBalanceRequest balanceRequest);
}