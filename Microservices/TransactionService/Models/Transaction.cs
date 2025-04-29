using System.ComponentModel.DataAnnotations.Schema;

namespace TransactionService.Models
{
    public class Transaction
    {
        public required string Id { get; set; }
        public required string TransferId { get; set; }
        public required string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public required string Description { get; set; }
        public required string Status { get; set; }
        public required string FromAccount { get; set; }
        public required string ToAccount { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Mark as NotMapped since it doesn't exist in the database
        [NotMapped]
        public string LastModifiedBy { get; set; } = "system";  // Default value
        
        // Already marked as NotMapped
        [NotMapped]
        public string? FraudCheckResult { get; set; }

        // Already marked as NotMapped
        [NotMapped]
        public string? ClientIp { get; set; }
    }
}
