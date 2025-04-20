using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Data;

namespace TransactionService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly ILogger<DiagnosticsController> _logger;
    private readonly string _connectionString;

    public DiagnosticsController(ILogger<DiagnosticsController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
            "Server=localhost;Database=transaction_db;User=root;Password=password;";
    }

    [HttpGet("transaction/{transferId}")]
    public async Task<IActionResult> GetRawTransaction(string transferId)
    {
        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Transactions WHERE TransferId = @TransferId";
            command.Parameters.AddWithValue("@TransferId", transferId);
            
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var columnMappings = new List<object>();
                
                // Get information about each column
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var columnInfo = new
                    {
                        Index = i,
                        Name = reader.GetName(i),
                        Type = reader.GetFieldType(i).FullName,
                        Value = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i).ToString(),
                        HexValue = reader.IsDBNull(i) ? "NULL" : 
                            (reader.GetFieldType(i) == typeof(byte[]) ? 
                                BitConverter.ToString((byte[])reader.GetValue(i)).Replace("-", "") : "N/A")
                    };
                    columnMappings.Add(columnInfo);
                }
                
                return Ok(new { 
                    Message = "Raw transaction data",
                    Columns = columnMappings
                });
            }
            
            return NotFound($"Transaction with transfer ID {transferId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in diagnostics for transfer ID: {TransferId}", transferId);
            return StatusCode(500, new { 
                Error = "An error occurred during diagnostics",
                Details = ex.Message,
                StackTrace = ex.StackTrace
            });
        }
    }
}