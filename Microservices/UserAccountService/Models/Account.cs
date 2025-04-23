namespace AccountService.Models;

public class Account
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}
