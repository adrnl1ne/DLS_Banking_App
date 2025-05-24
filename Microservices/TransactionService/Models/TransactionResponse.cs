namespace TransactionService.Models
{
    public class TransactionResponse
    {
        public string TransferId { get; init; } = string.Empty;
        public int? UserId { get; set; }
        public string FromAccount { get; init; } = string.Empty;
        public string ToAccount { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string StatusMessage { get; set; } = string.Empty;

        public static TransactionResponse FromTransaction(Transaction transaction, string statusMessage = "")
        {
            var response = new TransactionResponse
            {
                TransferId = transaction.TransferId,
                UserId = transaction.UserId,
                FromAccount = transaction.FromAccount,
                ToAccount = transaction.ToAccount,
                Amount = transaction.Amount,
                Status = transaction.Status,
                CreatedAt = transaction.CreatedAt,
                UpdatedAt = transaction.UpdatedAt,
                StatusMessage = statusMessage
            };

            return response;
        }
    }
}
