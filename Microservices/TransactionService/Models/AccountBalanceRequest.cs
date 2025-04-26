namespace TransactionService.Models;

public class AccountBalanceRequest
{
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "Transfer";
    
    public class RequestDetails
    {
        // Include any fields required by the UserAccountService
        public string Description { get; set; } = "Balance update from Transaction Service";
    }
    
    public RequestDetails Request { get; set; } = new RequestDetails();
}