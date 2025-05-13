using AccountService.Database.Data;
using AccountService.Repository;
using Microsoft.EntityFrameworkCore;
using UserAccountService.Models;

namespace UserAccountService.Repository;

/// <summary>
/// Repository for managing account data.
/// </summary>
public class AccountRepository(UserAccountDbContext context) : IAccountRepository
{
    /// <summary>
    /// Retrieves all accounts.
    /// </summary>
    /// <returns>A collection of Account objects.</returns>
    public async Task<IEnumerable<Account>> GetAllAccountsAsync()
    {
        return await context.Accounts
            .Include(a => a.User)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves accounts by user ID.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A collection of Account objects for the specified user.</returns>
    public async Task<IEnumerable<Account>> GetAccountsByUserIdAsync(int userId)
    {
        return await context.Accounts
            .Where(a => a.UserId == userId)
            .Include(a => a.User)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves an account by its ID.
    /// </summary>
    /// <param name="id">The ID of the account.</param>
    /// <returns>The Account object if found, otherwise null.</returns>
    public async Task<Account?> GetAccountByIdAsync(int id)
    {
        return await context.Accounts
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    /// <summary>
    /// Adds a new account.
    /// </summary>
    /// <param name="account">The Account object to add.</param>
    public async Task AddAccountAsync(Account account)
    {
        await context.Accounts.AddAsync(account);
    }

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    public async Task SaveChangesAsync()
    {
        await context.SaveChangesAsync();
    }
}
