using System;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;
using TransactionService.Services.Interface;

namespace TransactionService.Services
{
    public class AccountBalanceMessageService : IAccountBalanceService
    {
        private readonly ILogger<AccountBalanceMessageService> _logger;
        private readonly IRabbitMqClient _rabbitMqClient;
        private const string BALANCE_UPDATES_QUEUE = "AccountBalanceUpdates";

        public AccountBalanceMessageService(
            ILogger<AccountBalanceMessageService> logger,
            IRabbitMqClient rabbitMqClient)
        {
            _logger = logger;
            _rabbitMqClient = rabbitMqClient;
        }

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
                _rabbitMqClient.Publish(
                    BALANCE_UPDATES_QUEUE, 
                    JsonSerializer.Serialize(message)
                );
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing account balance update message");
                throw;
            }
        }
    }

    public class AccountBalanceUpdateMessage
    {
        public int AccountId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionId { get; set; }
        public string TransactionType { get; set; }
        public bool IsAdjustment { get; set; }
        public DateTime Timestamp { get; set; }
    }
}