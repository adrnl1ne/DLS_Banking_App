namespace TransactionService.Models;

public class AccountBalanceRequest
{
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty; // "Deposit" or "Withdrawal"
    
    // Add a nested class if needed by the API
    public class RequestDetails
    {
        public string Description { get; set; } = string.Empty;
    }
    
    public RequestDetails Request { get; set; } = new RequestDetails();
}