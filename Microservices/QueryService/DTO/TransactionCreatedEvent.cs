namespace QueryService.DTO;

public class TransactionCreatedEvent
{
    public string TransferId { get; set; }
    public string Status { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; }
    public int FromAccount { get; set; }
    public int ToAccount { get; set; }
    public string CreatedAt { get; set; }
}