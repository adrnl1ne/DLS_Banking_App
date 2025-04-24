using AccountService.Database.Data;
using AccountService.Models;
using AccountService.Repository;
using Microsoft.EntityFrameworkCore;
using UserAccountService.Models;

namespace UserAccountService.Repository;

public class AccountRepository(UserAccountDbContext context) : IAccountRepository
{
    public async Task<IEnumerable<Account>> GetAllAccountsAsync()
    {
        return await context.Accounts
            .Include(a => a.User)
            .ToListAsync();
    }

    public async Task<IEnumerable<Account>> GetAccountsByUserIdAsync(int userId)
    {
        return await context.Accounts
            .Where(a => a.UserId == userId)
            .Include(a => a.User)
            .ToListAsync();
    }

    public async Task<Account?> GetAccountByIdAsync(int id)
    {
        return await context.Accounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task AddAccountAsync(Account account)
    {
        await context.Accounts.AddAsync(account);
    }

    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
