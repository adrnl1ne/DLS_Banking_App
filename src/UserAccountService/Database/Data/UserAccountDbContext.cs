using Microsoft.EntityFrameworkCore;
using AccountService.Models;

namespace AccountService.Database.Data;

public class UserAccountDbContext(DbContextOptions<UserAccountDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Account> Accounts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map Account entity to account table
        modelBuilder.Entity<Account>()
            .ToTable("account")
            .Property(a => a.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd(); // Auto-increment
        modelBuilder.Entity<Account>()
            .Property(a => a.Name)
            .HasColumnName("name");
        modelBuilder.Entity<Account>()
            .Property(a => a.Amount)
            .HasColumnName("amount");
        modelBuilder.Entity<Account>()
            .Property(a => a.UserId)
            .HasColumnName("user_id");

        // Map User entity to user table
        modelBuilder.Entity<User>()
            .ToTable("user")
            .Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd(); // Auto-increment
        modelBuilder.Entity<User>()
            .Property(u => u.Username)
            .HasColumnName("username");
        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasColumnName("email");
        modelBuilder.Entity<User>()
            .Property(u => u.Password)
            .HasColumnName("password");
        modelBuilder.Entity<User>()
            .Property(u => u.CreatedAt)
            .HasColumnName("created_at");
        modelBuilder.Entity<User>()
            .Property(u => u.UpdatedAt)
            .HasColumnName("updated_at");
        modelBuilder.Entity<User>()
            .Property(u => u.RoleId)
            .HasColumnName("role_id");

        // Map Role entity to role table
        modelBuilder.Entity<Role>()
            .ToTable("role")
            .Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd(); // Auto-increment
        modelBuilder.Entity<Role>()
            .Property(r => r.Name)
            .HasColumnName("name");

        // Configure relationships
        modelBuilder.Entity<Account>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId);

        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany()
            .HasForeignKey(u => u.RoleId);

        // Configure unique constraints
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();
    }
}
