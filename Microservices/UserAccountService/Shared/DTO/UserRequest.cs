namespace UserAccountService.Shared.DTO;

/// <summary>
/// Represents a request to create a new user.
/// </summary>
public class UserRequest
{
    /// <summary>
    /// Gets or sets the username for the new user.
    /// </summary>
    public required string Username { get; set; }

    /// <summary>
    /// Gets or sets the email address for the new user.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Gets or sets the password for the new user.
    /// </summary>
    public required string Password { get; set; }

    /// <summary>
    /// Gets or sets the role ID for the new user.
    /// </summary>
    public int RoleId { get; set; }
} 