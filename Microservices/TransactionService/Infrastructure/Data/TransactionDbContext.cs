using Microsoft.EntityFrameworkCore;
using TransactionService.Models;

namespace TransactionService.Infrastructure.Data;

public class TransactionDbContext : DbContext
{
    public TransactionDbContext(DbContextOptions<TransactionDbContext> options)
        : base(options)
    {
    }

    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<TransactionLog> TransactionLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TransferId).IsUnique();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
        });
        
        modelBuilder.Entity<TransactionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TransactionId);
            entity.Property(e => e.ContainsSensitiveData).HasDefaultValue(false);
        });
    }
}