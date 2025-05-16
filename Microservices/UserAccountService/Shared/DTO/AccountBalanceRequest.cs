﻿namespace UserAccountService.Shared.DTO;

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
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transaction type for the balance update.
    /// </summary>
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this instance is an adjustment.
    /// </summary>
    public bool IsAdjustment { get; set; }
}