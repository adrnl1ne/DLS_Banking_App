using System;
using Microsoft.Extensions.Logging;
using TransactionService.Models; // Add this namespace for FraudResult

namespace TransactionService.Services;

/// <summary>
/// Fallback service for when the Fraud Detection Service is unavailable
/// </summary>
public class FallbackFraudService
{
    private readonly ILogger<FallbackFraudService> _logger;
    private readonly Random _random = new Random();

    public FallbackFraudService(ILogger<FallbackFraudService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Performs a basic fraud check as a fallback
    /// </summary>
    public FraudResult CheckFraud(string transferId, decimal amount, string fromAccount, string toAccount)
    {
        _logger.LogWarning("Using fallback fraud detection for transfer {TransferId}", transferId);
        
        // Simple rules to mimic basic fraud detection
        bool isFraud = false;
        
        // Large amounts get more scrutiny
        if (amount > 1000)
        {
            // 10% chance of flagging as fraud for amounts over 1000
            isFraud = _random.Next(10) == 0;
        }
        
        // Add some variability for demo purposes (0.5% chance for any transaction)
        if (_random.Next(200) == 0)
        {
            isFraud = true;
        }
        
        _logger.LogInformation("Fallback fraud check for transfer {TransferId}: {IsFraud}", transferId, isFraud);
        
        return new FraudResult
        {
            TransferId = transferId,
            IsFraud = isFraud,
            Status = isFraud
                ? "declined"
                : "approved",
            Timestamp = DateTime.UtcNow.ToString("o"),
        };
    }
}