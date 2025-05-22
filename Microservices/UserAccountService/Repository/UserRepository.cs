using AccountService.Database.Data;
using Microsoft.EntityFrameworkCore;
using UserAccountService.Models;

namespace UserAccountService.Repository;

/// <summary>
/// Repository for managing user data.
/// </summary>
public class UserRepository(UserAccountDbContext context) : IUserRepository
{
    /// <summary>
    /// Retrieves a user by their email address.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <returns>The User object if found, otherwise null.</returns>
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    /// <param name="username">The username of the user.</param>
    /// <returns>The User object if found, otherwise null.</returns>
    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    /// <summary>
    /// Retrieves all users from the database.
    /// </summary>
    /// <returns>A list of all User objects.</returns>
    public async Task<List<User>> GetAllUsersAsync()
    {
        return await context.Users
            .Include(u => u.Role)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new user in the database.
    /// </summary>
    /// <param name="user">The User object to create.</param>
    public async Task CreateUserAsync(User user)
    {
        await context.Users.AddAsync(user);
        await context.SaveChangesAsync();
    }
}
