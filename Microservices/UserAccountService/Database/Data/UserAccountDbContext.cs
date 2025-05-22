using Microsoft.EntityFrameworkCore;
using UserAccountService.Models;

namespace AccountService.Database.Data;

/// <summary>
/// Represents the database context for the User Account Service.
/// </summary>
public class UserAccountDbContext(DbContextOptions<UserAccountDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets or sets the DbSet for Roles.
    /// </summary>
    public DbSet<Role> Roles { get; set; }

    /// <summary>
    /// Gets or sets the DbSet for Users.
    /// </summary>
    public DbSet<User> Users { get; set; }

    /// <summary>
    /// Gets or sets the DbSet for Accounts.
    /// </summary>
    public DbSet<Account> Accounts { get; set; }
	public DbSet<DeletedAccount> DeletedAccounts { get; set; }

    /// <summary>
	/// Configures the model using the provided builder.
	/// </summary>
	/// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
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

		// Map DeletedAccount entity to deleted_account table
		modelBuilder.Entity<DeletedAccount>()
			.ToTable("deleted_account")
			.Property(d => d.Id)
			.HasColumnName("id")
			.ValueGeneratedOnAdd(); // Auto-increment
		modelBuilder.Entity<DeletedAccount>()
			.Property(d => d.AccountId)
			.HasColumnName("account_id");
		modelBuilder.Entity<DeletedAccount>()
			.Property(d => d.UserId)
			.HasColumnName("user_id");
		modelBuilder.Entity<DeletedAccount>()
			.Property(d => d.Name)
			.HasColumnName("name");
		modelBuilder.Entity<DeletedAccount>()
			.Property(d => d.Amount)
			.HasColumnName("amount");
		modelBuilder.Entity<DeletedAccount>()
			.Property(d => d.DeletedAt)
			.HasColumnName("deleted_at");

		// Configure unique constraints
		modelBuilder.Entity<User>()
			.HasIndex(u => u.Email)
			.IsUnique();
		modelBuilder.Entity<User>()
			.HasIndex(u => u.Username)
			.IsUnique();
	}
}
