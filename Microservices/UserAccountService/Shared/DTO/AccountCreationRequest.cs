namespace UserAccountService.Shared.DTO;

/// <summary>
/// Represents a request to create a new account.
/// </summary>
public class AccountCreationRequest
{
    /// <summary>
    /// Gets or sets the name for the new account.
    /// </summary>
    public required string Name { get; set; }
}
