using System;

namespace UserAccountService.Models;

public class DeletedAccount
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateTime DeletedAt { get; set; }
}