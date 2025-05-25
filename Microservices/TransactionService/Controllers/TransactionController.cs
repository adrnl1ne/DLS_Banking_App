using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prometheus;
using TransactionService.Models;
using TransactionService.Services.Interface;

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
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                TransactionErrorsTotal.WithLabels("POST").Inc();
                return Unauthorized("User ID not found in token.");
            }

            request.UserId = userId; // Set the user ID from the token

            logger.LogInformation("Creating transfer");

            var result = await transactionService.CreateTransferAsync(request);

            // Add status message to response if it exists
            if (!string.IsNullOrEmpty(result.StatusMessage))
            {
                Response.Headers.Add("X-Transaction-Status", result.StatusMessage);
            }

            return CreatedAtAction(nameof(GetTransaction), new { transferId = result.TransferId }, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            TransactionErrorsTotal.WithLabels("POST").Inc();
            logger.LogWarning("Unauthorized access attempt");
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            TransactionErrorsTotal.WithLabels("POST").Inc();
            logger.LogWarning("Invalid operation request");
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
            transferId = transferId.Replace("\n", "").Replace("\r", "") ?? throw new InvalidOperationException();

            logger.LogInformation("Getting transaction");

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
            logger.LogInformation("Getting transactions");

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
            logger.LogWarning("Unauthorized access attempt");
            return StatusCode(403, ex.Message);
        }
        catch (ArgumentException ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            logger.LogWarning("Invalid argument provided");
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            logger.LogWarning("Invalid operation request");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            logger.LogError(ex, $"Error retrieving transactions for account");
            return StatusCode(500, "An error occurred while retrieving transactions: {ex.Message}");
        }
    }
}