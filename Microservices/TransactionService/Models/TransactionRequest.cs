namespace TransactionService.Models;

public class TransactionRequest
{
    public required string FromAccount { get; set; }
    public required string ToAccount { get; set; }
    public decimal Amount { get; set; }
    public int UserId { get; set; }
}