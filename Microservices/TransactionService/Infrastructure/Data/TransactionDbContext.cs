using Microsoft.EntityFrameworkCore;
using TransactionService.Models;

namespace TransactionService.Infrastructure.Data;

public class TransactionDbContext(DbContextOptions<TransactionDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TransferId).IsUnique();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
        });
    }
}