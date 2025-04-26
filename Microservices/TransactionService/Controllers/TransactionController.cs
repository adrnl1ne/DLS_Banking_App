using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;
using TransactionService.Models;
using TransactionService.Services;

namespace TransactionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionController(ITransactionService transactionService, ILogger<TransactionController> logger)
    : ControllerBase
{
    // Prometheus metrics
    private static readonly Counter TransactionRequestsTotal = Metrics.CreateCounter(
        "transaction_requests_total",
        "Total number of transaction requests",
        new CounterConfiguration { LabelNames = new[] { "method" } }
    );
    private static readonly Counter TransactionErrorsTotal = Metrics.CreateCounter(
        "transaction_errors_total",
        "Total number of transaction errors",
        new CounterConfiguration { LabelNames = new[] { "method" } }
    );

    [HttpPost("transfer")]
    [Authorize]
    public async Task<ActionResult<TransactionResponse>> CreateTransfer([FromBody] TransactionRequest request)
    {
        TransactionRequestsTotal.WithLabels("POST").Inc();

        try
        {
            if (request.Amount <= 0)
            {
                TransactionErrorsTotal.WithLabels("POST").Inc();
                return BadRequest("Amount must be greater than zero");
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                TransactionErrorsTotal.WithLabels("POST").Inc();
                return Unauthorized("User ID not found in token.");
            }

            request.UserId = userId; // Set the user ID from the token

            logger.LogInformation($"Creating transfer from account {request.FromAccount} to account {request.ToAccount} for amount {request.Amount}");

            var result = await transactionService.CreateTransferAsync(request);
            return CreatedAtAction(nameof(GetTransaction), new { transferId = result.TransferId }, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            TransactionErrorsTotal.WithLabels("POST").Inc();
            logger.LogWarning(ex, "Unauthorized access attempt during transfer creation");
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            TransactionErrorsTotal.WithLabels("POST").Inc();
            logger.LogWarning(ex, "Invalid operation during transfer creation");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            TransactionErrorsTotal.WithLabels("POST").Inc();
            logger.LogError(ex, "Error creating transfer");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("{transferId}")]
    [Authorize]
    public async Task<ActionResult<TransactionResponse>> GetTransaction(string transferId)
    {
        TransactionRequestsTotal.WithLabels("GET").Inc();

        try
        {
            // Sanitize transferId to prevent log forging
            transferId = transferId?.Replace("\n", "").Replace("\r", "");

            logger.LogInformation($"Getting transaction with ID: {transferId}");

            if (string.IsNullOrEmpty(transferId))
            {
                TransactionErrorsTotal.WithLabels("GET").Inc();
                return BadRequest("Transfer ID cannot be empty");
            }

            var transaction = await transactionService.GetTransactionByTransferIdAsync(transferId);

            if (transaction == null)
            {
                logger.LogWarning($"Transaction not found with ID: {transferId}");
                return NotFound($"Transaction with ID {transferId} not found");
            }

            return Ok(transaction);
        }
        catch (Exception ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            logger.LogError(ex, $"Error retrieving transaction {transferId}");
            return StatusCode(500, $"An error occurred while retrieving the transaction: {ex.Message}");
        }
    }

    [HttpGet("account/{accountId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetTransactionsByAccount(string accountId)
    {
        TransactionRequestsTotal.WithLabels("GET").Inc();

        try
        {
            // Sanitize accountId to prevent log forging
            accountId = accountId.Replace("\n", "").Replace("\r", "");
            var hashedAccountId = HashSensitiveData(accountId);
            logger.LogInformation($"Getting transactions for account with hashed ID: {hashedAccountId}");

            if (string.IsNullOrEmpty(accountId))
            {
                TransactionErrorsTotal.WithLabels("GET").Inc();
                return BadRequest("Account ID cannot be empty");
            }

            // Extract the authenticated user's ID from the JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                TransactionErrorsTotal.WithLabels("GET").Inc();
                return Unauthorized("User ID not found in token.");
            }

            var transactions = await transactionService.GetTransactionsByAccountAsync(accountId, userId);

            return Ok(transactions);
        }
        catch (UnauthorizedAccessException ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            logger.LogWarning(ex, "Unauthorized access attempt during retrieval of transactions for account {AccountId}", accountId);
            return StatusCode(403, ex.Message);
        }
        catch (ArgumentException ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            logger.LogWarning(ex, "Invalid argument during retrieval of transactions for account {AccountId}", accountId);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            logger.LogWarning(ex, "Invalid operation during retrieval of transactions for account {AccountId}", accountId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            logger.LogError(ex, $"Error retrieving transactions for account {accountId}");
            return StatusCode(500, $"An error occurred while retrieving transactions: {ex.Message}");
        }
    }
    private string HashSensitiveData(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return string.Empty;
        }
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashedBytes);
    }
}
