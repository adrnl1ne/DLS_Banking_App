using System.ComponentModel.DataAnnotations;

namespace TransactionService.Models;

public class TransactionRequest
{
    [Required]
    public int UserId { get; set; }

    [Required]
    public required string FromAccount { get; set; }
        
    [Required]
    public required string ToAccount { get; set; }
        
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }
        
    public string? Description { get; set; }
        
}