using AccountService.Models;
using UserAccountService.Models;

namespace AccountService.Repository;

public interface IAccountRepository
{
    Task<IEnumerable<Account>> GetAllAccountsAsync();
    Task<IEnumerable<Account>> GetAccountsByUserIdAsync(int userId);
    Task<Account?> GetAccountByIdAsync(int id);
    Task AddAccountAsync(Account account);
    Task SaveChangesAsync();
}
