using System.ComponentModel.DataAnnotations.Schema;

namespace TransactionService.Models
{
    public class Transaction
    {
        public string Id { get; set; }
        public string TransferId { get; set; }
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string FromAccount { get; set; }
        public string ToAccount { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Mark as NotMapped since it doesn't exist in the database
        [NotMapped]
        public string LastModifiedBy { get; set; } = "system";  // Default value
        
        // Already marked as NotMapped
        [NotMapped]
        public string FraudCheckResult { get; set; }

        // Already marked as NotMapped
        [NotMapped]
        public string ClientIp { get; set; }
    }
}
