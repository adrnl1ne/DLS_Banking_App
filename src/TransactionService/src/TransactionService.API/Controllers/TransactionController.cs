using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransactionService.API.Models;
using TransactionService.API.Services;

namespace TransactionService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(ITransactionService transactionService, ILogger<TransactionController> logger)
    {
        _transactionService = transactionService;
        _logger = logger;
    }

    [HttpPost("transfer")]
    [Authorize]
    public async Task<ActionResult<TransactionResponse>> CreateTransfer([FromBody] TransactionRequest request)
    {
        try
        {
            if (request.Amount <= 0)
            {
                return BadRequest("Amount must be greater than zero");
            }
            
            _logger.LogInformation($"Creating transfer from {request.FromAccount} to {request.ToAccount} for {request.Amount}");
            
            var result = await _transactionService.CreateTransferAsync(request);
            return CreatedAtAction(nameof(GetTransaction), new { transferId = result.TransferId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transfer");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("{transferId}")]
    [Authorize]
    public async Task<ActionResult<TransactionResponse>> GetTransaction(string transferId)
    {
        try
        {
            _logger.LogInformation($"Getting transaction with ID: {transferId}");
            
            if (string.IsNullOrEmpty(transferId))
            {
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
            _logger.LogError(ex, $"Error retrieving transaction {transferId}");
            return StatusCode(500, $"An error occurred while retrieving the transaction: {ex.Message}");
        }
    }

    [HttpGet("account/{accountId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetTransactionsByAccount(string accountId)
    {
        try
        {
            _logger.LogInformation($"Getting transactions for account: {accountId}");
            
            if (string.IsNullOrEmpty(accountId))
            {
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
            _logger.LogError(ex, $"Error retrieving transactions for account {accountId}");
            return StatusCode(500, $"An error occurred while retrieving transactions: {ex.Message}");
        }
    }
}