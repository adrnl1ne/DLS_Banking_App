namespace UserAccountService.Shared.DTO;

/// <summary>
/// Represents a request to rename an account.
/// </summary>
public class AccountRenameRequest
{
    /// <summary>
    /// Gets or sets the new name for the account.
    /// </summary>
    public required string Name { get; set; }
}