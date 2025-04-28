namespace UserAccountService.Shared.DTO;

public class AccountBalanceRequest
{
    public decimal Amount { get; set; }
    public required string TransactionId { get; set; }
    public required string  TransactionType { get; set; }
}