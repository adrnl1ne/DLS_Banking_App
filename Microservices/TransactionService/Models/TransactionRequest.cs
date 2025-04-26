using System.ComponentModel.DataAnnotations;

namespace TransactionService.Models
{
    public class TransactionRequest
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string FromAccount { get; set; } = string.Empty;
        
        [Required]
        public string ToAccount { get; set; } = string.Empty;
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        public string? Description { get; set; }
        
        // Default to "transfer" if not specified
        public string TransactionType { get; set; } = "transfer";
        
        // Add this property back to fix the deserialization error
        public string? Currency { get; set; } = "USD";
    }
}