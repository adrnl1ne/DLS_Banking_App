using UserAccountService.Models;

namespace AccountService.Repository;

/// <summary>
/// Interface for managing account data.
/// </summary>
public interface IAccountRepository
{
    /// <summary>
    /// Retrieves all accounts.
    /// </summary>
    /// <returns>A collection of Account objects.</returns>
    Task<IEnumerable<Account>> GetAllAccountsAsync();

    /// <summary>
    /// Retrieves accounts by user ID.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>A collection of Account objects for the specified user.</returns>
    Task<IEnumerable<Account>> GetAccountsByUserIdAsync(int userId);

    /// <summary>
    /// Retrieves an account by its ID.
    /// </summary>
    /// <param name="id">The ID of the account.</param>
    /// <returns>The Account object if found, otherwise null.</returns>
    Task<Account?> GetAccountByIdAsync(int id);

    /// <summary>
    /// Adds a new account.
    /// </summary>
    /// <param name="account">The Account object to add.</param>
    Task AddAccountAsync(Account account);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync();
}
