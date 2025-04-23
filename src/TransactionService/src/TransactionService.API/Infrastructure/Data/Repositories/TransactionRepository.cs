using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using TransactionService.API.Models;

namespace TransactionService.API.Infrastructure.Data.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly TransactionDbContext _context;
    private readonly ILogger<TransactionRepository> _logger;
    private readonly string _connectionString;

    private static string HashAccountId(string accountId)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(accountId);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public TransactionRepository(
        TransactionDbContext context, 
        ILogger<TransactionRepository> logger,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
            "Server=localhost;Database=transaction_db;User=root;Password=password;";
    }

    public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
    {
        try
        {
            await _context.Transactions.AddAsync(transaction);
            await _context.SaveChangesAsync();
            return transaction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transaction: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<Transaction?> GetTransactionByIdAsync(Guid id)
    {
        return await _context.Transactions.FindAsync(id);
    }

    public async Task<Transaction?> GetTransactionByTransferIdAsync(string transferId)
    {
        try
        {
            _logger.LogInformation("Getting transaction with transfer ID: {TransferId}", transferId);
            
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM Transactions 
                WHERE TransferId = @TransferId";
            command.Parameters.AddWithValue("@TransferId", transferId);
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                // Read each field properly based on its type
                var transaction = new Transaction
                {
                    // For GUID, try different approaches based on how it's stored
                    Id = ReadGuidFromReader(reader, "Id"),
                    TransferId = reader.GetString("TransferId"),
                    FromAccount = reader.GetString("FromAccount"),
                    ToAccount = reader.GetString("ToAccount"),
                    Amount = reader.GetDecimal("Amount"),
                    Status = reader.GetString("Status"),
                    CreatedAt = reader.GetDateTime("CreatedAt")
                };

                // Handle nullable field
                if (!reader.IsDBNull(reader.GetOrdinal("UpdatedAt")))
                {
                    transaction.UpdatedAt = reader.GetDateTime("UpdatedAt");
                }
                
                _logger.LogInformation("Found transaction with ID: {Id}", transaction.Id);
                return transaction;
            }
            
            _logger.LogWarning("No transaction found with transfer ID: {TransferId}", transferId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction with transfer ID: {TransferId}. Error: {Message}", 
                transferId, ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<Transaction>> GetTransactionsByAccountAsync(string accountId)
    {
        try
        {
            var sanitizedAccountId = accountId.Replace("\n", "").Replace("\r", "");
            var hashedAccountId = HashAccountId(sanitizedAccountId);
            _logger.LogInformation("Getting transactions for account: {AccountId}", hashedAccountId);
            
            var transactions = new List<Transaction>();
            
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM Transactions 
                WHERE FromAccount = @AccountId OR ToAccount = @AccountId";
            command.Parameters.AddWithValue("@AccountId", accountId);
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var transaction = new Transaction
                {
                    // Use the same helper method for GUID
                    Id = ReadGuidFromReader(reader, "Id"),
                    TransferId = reader.GetString("TransferId"),
                    FromAccount = reader.GetString("FromAccount"),
                    ToAccount = reader.GetString("ToAccount"),
                    Amount = reader.GetDecimal("Amount"),
                    Status = reader.GetString("Status"),
                    CreatedAt = reader.GetDateTime("CreatedAt")
                };

                // Handle nullable field
                if (!reader.IsDBNull(reader.GetOrdinal("UpdatedAt")))
                {
                    transaction.UpdatedAt = reader.GetDateTime("UpdatedAt");
                }
                
                transactions.Add(transaction);
            }
            
            _logger.LogInformation("Found {Count} transactions for account: {AccountId}", 
                transactions.Count, sanitizedAccountId);
            return transactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions for account: {AccountId}. Error: {Message}", 
                sanitizedAccountId, ex.Message);
            throw;
        }
    }

    public async Task<Transaction> UpdateTransactionStatusAsync(string transferId, string status)
    {
        try
        {
            _logger.LogInformation("Updating transaction {TransferId} status to {Status}", transferId, status);
            
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            // First update the record
            using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = @"
                UPDATE Transactions 
                SET Status = @Status, UpdatedAt = @UpdatedAt
                WHERE TransferId = @TransferId";
            updateCommand.Parameters.AddWithValue("@Status", status);
            updateCommand.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow);
            updateCommand.Parameters.AddWithValue("@TransferId", transferId);
            
            var rowsAffected = await updateCommand.ExecuteNonQueryAsync();
            _logger.LogInformation("Updated {RowsAffected} rows for transaction {TransferId}", 
                rowsAffected, transferId);
            
            if (rowsAffected == 0)
            {
                _logger.LogWarning("No transaction found with transfer ID: {TransferId}", transferId);
                throw new KeyNotFoundException($"Transaction with ID {transferId} not found");
            }
            
            // Then retrieve the updated record
            using var selectCommand = connection.CreateCommand();
            selectCommand.CommandText = @"
                SELECT * FROM Transactions 
                WHERE TransferId = @TransferId";
            selectCommand.Parameters.AddWithValue("@TransferId", transferId);
            
            using var reader = await selectCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var transaction = new Transaction
                {
                    Id = ReadGuidFromReader(reader, "Id"),
                    TransferId = reader.GetString("TransferId"),
                    FromAccount = reader.GetString("FromAccount"),
                    ToAccount = reader.GetString("ToAccount"),
                    Amount = reader.GetDecimal("Amount"),
                    Status = reader.GetString("Status"),
                    CreatedAt = reader.GetDateTime("CreatedAt")
                };

                // Handle nullable field
                if (!reader.IsDBNull(reader.GetOrdinal("UpdatedAt")))
                {
                    transaction.UpdatedAt = reader.GetDateTime("UpdatedAt");
                }
                
                _logger.LogInformation("Retrieved updated transaction: {Id}", transaction.Id);
                return transaction;
            }
            
            // This should not happen since we checked rowsAffected > 0
            throw new InvalidOperationException($"Could not retrieve transaction after updating: {transferId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating transaction status for transfer ID: {TransferId}. Error: {Message}", 
                transferId, ex.Message);
            throw;
        }
    }

    public async Task<bool> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync() > 0;
    }

    // Helper method to handle different possible GUID storage formats
    private Guid ReadGuidFromReader(MySqlDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            
            // Try to read as string and parse
            if (!reader.IsDBNull(ordinal))
            {
                // Check the field type
                var fieldType = reader.GetFieldType(ordinal);
                _logger.LogDebug("Field {Field} is of type {Type}", columnName, fieldType.Name);
                
                if (fieldType == typeof(Guid))
                {
                    return reader.GetGuid(ordinal);
                }
                else if (fieldType == typeof(string))
                {
                    string guidString = reader.GetString(ordinal);
                    return Guid.Parse(guidString);
                }
                else if (fieldType == typeof(byte[]))
                {
                    byte[] bytes = (byte[])reader.GetValue(ordinal);
                    return new Guid(bytes);
                }
                else
                {
                    // Try with ToString
                    string? guidString = reader.GetValue(ordinal)?.ToString();
                    if (!string.IsNullOrEmpty(guidString))
                    {
                        return Guid.Parse(guidString);
                    }
                }
            }
            
            // Default to empty GUID if all else fails
            _logger.LogWarning("Could not read GUID for {Column}, using empty GUID", columnName);
            return Guid.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading GUID from column {Column}", columnName);
            // Return empty GUID in case of error - you might want to throw instead
            return Guid.Empty;
        }
    }
}