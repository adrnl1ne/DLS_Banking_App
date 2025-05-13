namespace UserAccountService.Models;

/// <summary>
/// Represents a role in the system.
/// </summary>
public class Role
{
    /// <summary>
    /// Gets or sets the role ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the role name.
    /// </summary>
    public required string Name { get; set; }
}
