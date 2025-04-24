namespace UserAccountService.Shared.DTO;

public class AccountResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int UserId { get; set; }
}
