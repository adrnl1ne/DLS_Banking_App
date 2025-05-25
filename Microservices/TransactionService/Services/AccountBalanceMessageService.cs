using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;
using TransactionService.Services.Interface;

namespace TransactionService.Services
{
    public class AccountBalanceMessageService(
        ILogger<AccountBalanceMessageService> logger,
        IRabbitMqClient rabbitMqClient)
        : IAccountBalanceService
    {
        private const string BALANCE_UPDATES_QUEUE = "AccountBalanceUpdates";

        public Task UpdateBalanceAsync(int accountId, AccountBalanceRequest balanceRequest)
        {
            var message = new AccountBalanceUpdateMessage
            {
                AccountId = accountId,
                Amount = balanceRequest.Amount,
                TransactionId = balanceRequest.TransactionId,
                TransactionType = balanceRequest.TransactionType,
                IsAdjustment = balanceRequest.IsAdjustment,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                rabbitMqClient.Publish(
                    BALANCE_UPDATES_QUEUE, 
                    JsonSerializer.Serialize(message)
                );
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing account balance update message");
                throw;
            }
        }
    }

    public class AccountBalanceUpdateMessage
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionId { get; set; } = string.Empty;
        public string TransactionType { get; set; } = string.Empty;
        public bool IsAdjustment { get; set; }
        public DateTime Timestamp { get; set; }
    }
}