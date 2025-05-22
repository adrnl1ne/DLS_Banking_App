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

    /// <summary>
    /// Retrieves all users from the database.
    /// </summary>
    /// <returns>A list of all User objects.</returns>
    Task<List<User>> GetAllUsersAsync();

    /// <summary>
    /// Creates a new user in the database.
    /// </summary>
    /// <param name="user">The User object to create.</param>
    Task CreateUserAsync(User user);
}
