using UserAccountService.Models;

namespace UserAccountService.Repository;

/// <summary>
/// Interface for managing user data.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves a user by their email address.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <returns>The User object if found, otherwise null.</returns>
    Task<User?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Retrieves a user by their username.
    /// </summary>
    /// <param name="username">The username of the user.</param>
    /// <returns>The User object if found, otherwise null.</returns>
    Task<User?> GetUserByUsernameAsync(string username);
}
