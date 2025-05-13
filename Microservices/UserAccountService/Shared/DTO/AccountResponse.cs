namespace UserAccountService.Shared.DTO;

/// <summary>
/// Represents the response model for an account.
/// </summary>
public class AccountResponse
{
    /// <summary>
    /// Gets or sets the account ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the account name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account balance.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the user ID associated with the account.
    /// </summary>
    public int UserId { get; set; }
}
