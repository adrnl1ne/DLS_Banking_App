using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Prometheus;
using TransactionService.Models;
using TransactionService.Services;
using System.Security.Claims;

namespace TransactionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ILogger<TransactionController> _logger;

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

    public TransactionController(ITransactionService transactionService, ILogger<TransactionController> logger)
    {
        _transactionService = transactionService;
        _logger = logger;
    }

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

            _logger.LogInformation($"Creating transfer from account {request.FromAccount} to account {request.ToAccount} for amount {request.Amount}");

            var result = await _transactionService.CreateTransferAsync(request);
            return CreatedAtAction(nameof(GetTransaction), new { transferId = result.TransferId }, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            TransactionErrorsTotal.WithLabels("POST").Inc();
            _logger.LogWarning(ex, "Unauthorized access attempt during transfer creation");
            return Unauthorized(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            TransactionErrorsTotal.WithLabels("POST").Inc();
            _logger.LogWarning(ex, "Invalid operation during transfer creation");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            TransactionErrorsTotal.WithLabels("POST").Inc();
            _logger.LogError(ex, "Error creating transfer");
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
            _logger.LogInformation($"Getting transaction with ID: {transferId}");

            if (string.IsNullOrEmpty(transferId))
            {
                TransactionErrorsTotal.WithLabels("GET").Inc();
                return BadRequest("Transfer ID cannot be empty");
            }

            var transaction = await _transactionService.GetTransactionByTransferIdAsync(transferId);

            if (transaction == null)
            {
                _logger.LogWarning($"Transaction not found with ID: {transferId}");
                return NotFound($"Transaction with ID {transferId} not found");
            }

            return Ok(transaction);
        }
        catch (Exception ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            _logger.LogError(ex, $"Error retrieving transaction {transferId}");
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
            _logger.LogInformation($"Getting transactions for account: {accountId}");

            if (string.IsNullOrEmpty(accountId))
            {
                TransactionErrorsTotal.WithLabels("GET").Inc();
                return BadRequest("Account ID cannot be empty");
            }

            var transactions = await _transactionService.GetTransactionsByAccountAsync(accountId);

            if (transactions == null)
            {
                _logger.LogWarning($"No transactions found for account: {accountId}");
                return Ok(Array.Empty<TransactionResponse>());
            }

            return Ok(transactions);
        }
        catch (Exception ex)
        {
            TransactionErrorsTotal.WithLabels("GET").Inc();
            _logger.LogError(ex, $"Error retrieving transactions for account {accountId}");
            return StatusCode(500, $"An error occurred while retrieving transactions: {ex.Message}");
        }
    }
}
