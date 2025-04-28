namespace QueryService.DTO;

public class AccountDocument
{
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; }
    public decimal Amount { get; set; }
    public string LastUpdated { get; set; }
    public string LastTransactionType { get; set; }
}