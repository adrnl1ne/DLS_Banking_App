namespace QueryService.DTO;

public class TransactionDocument
{
    public string TransactionId { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; } // Final balance or transaction amount
    public string TransactionType { get; set; }
    
    public decimal FinalBalance { get; set; }
    public string Timestamp { get; set; }
}