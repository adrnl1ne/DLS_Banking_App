using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransactionService.API.Models;
using TransactionService.API.Services;

namespace TransactionService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionController(ITransactionService transactionService, ILogger<TransactionController> logger)
    : ControllerBase
{
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
            
            var sanitizedFromAccount = request.FromAccount?.Replace("\n", "").Replace("\r", "");
            var sanitizedToAccount = request.ToAccount?.Replace("\n", "").Replace("\r", "");
            var sanitizedAmount = request.Amount.ToString().Replace("\n", "").Replace("\r", "");
            logger.LogInformation($"Creating transfer from {sanitizedFromAccount} to {sanitizedToAccount} for {sanitizedAmount}");
            
            var result = await transactionService.CreateTransferAsync(request);
            return CreatedAtAction(nameof(GetTransaction), new { transferId = result.TransferId }, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating transfer");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpGet("{transferId}")]
    [Authorize]
    public async Task<ActionResult<TransactionResponse>> GetTransaction(string transferId)
    {
        try
        {
            var sanitizedTransferId = transferId?.Replace("\n", "").Replace("\r", "");
            logger.LogInformation($"Getting transaction with ID: {sanitizedTransferId}");
            
            if (string.IsNullOrEmpty(transferId))
            {
                return BadRequest("Transfer ID cannot be empty");
            }
            
            var transaction = await transactionService.GetTransactionByTransferIdAsync(transferId);
            
            if (transaction == null)
            {
                sanitizedTransferId = transferId?.Replace("\n", "").Replace("\r", "");
                logger.LogWarning($"Transaction not found with ID: {sanitizedTransferId}");
                return NotFound($"Transaction with ID {transferId} not found");
            }
            
            return Ok(transaction);
        }
        catch (Exception ex)
        {
            var sanitizedTransferId = transferId?.Replace("\n", "").Replace("\r", "");
            logger.LogError(ex, $"Error retrieving transaction {sanitizedTransferId}");
            return StatusCode(500, $"An error occurred while retrieving the transaction: {ex.Message}");
        }
    }

    [HttpGet("account/{accountId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetTransactionsByAccount(string accountId)
    {
        try
        {
            var hashedAccountId = HashSensitiveData(accountId);
            logger.LogInformation($"Getting transactions for account: {hashedAccountId}");
            
            if (string.IsNullOrEmpty(accountId))
            {
                return BadRequest("Account ID cannot be empty");
            }
            
            var transactions = await transactionService.GetTransactionsByAccountAsync(accountId);
            
            if (transactions == null)
            {
                logger.LogWarning($"No transactions found for account: {hashedAccountId}");
                return Ok(Array.Empty<TransactionResponse>());
            }
            
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            var hashedAccountId = HashSensitiveData(accountId);
            logger.LogError(ex, $"Error retrieving transactions for account {hashedAccountId}");
            return StatusCode(500, $"An error occurred while retrieving transactions: {ex.Message}");
        }
    }
}
