namespace TransactionService.Models;

public class AccountBalanceRequest
{
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
}