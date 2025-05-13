namespace UserAccountService.Shared.DTO;

/// <summary>
/// Represents a request to deposit funds into an account.
/// </summary>
public class AccountDepositRequest
{
    /// <summary>
    /// Gets or sets the amount to deposit.
    /// </summary>
    public decimal Amount { get; set; }
}