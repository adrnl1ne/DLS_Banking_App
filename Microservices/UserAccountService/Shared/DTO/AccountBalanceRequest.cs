namespace UserAccountService.Shared.DTO;

/// <summary>
/// Represents a request to update the balance of an account.
/// </summary>
public class AccountBalanceRequest
{
    /// <summary>
    /// Gets or sets the amount to update the balance with.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the transaction ID for the balance update.
    /// </summary>
    public required string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the transaction type for the balance update.
    /// </summary>
    public required string  TransactionType { get; set; }
}