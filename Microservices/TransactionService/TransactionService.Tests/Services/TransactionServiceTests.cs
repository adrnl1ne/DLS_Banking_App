using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Prometheus;
using TransactionService.Clients;
using TransactionService.Infrastructure.Data;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;
using TransactionService.Services;
using Xunit;

namespace TransactionService.Tests.Services
{
    public class TransactionServiceTests
    {
        private readonly Mock<ITransactionRepository> _mockRepository;
        private readonly Mock<IRabbitMQClient> _mockRabbitMqClient;
        private readonly Mock<ILogger<global::TransactionService.Services.TransactionService>> _mockLogger;
        private readonly Mock<Counter> _mockCounter;
        private readonly Mock<Histogram> _mockHistogram;
        private readonly global::TransactionService.Services.TransactionService _service;

        public TransactionServiceTests()
        {
            _mockRepository = new Mock<ITransactionRepository>();
            _mockRabbitMqClient = new Mock<IRabbitMQClient>();
            _mockLogger = new Mock<ILogger<global::TransactionService.Services.TransactionService>>();
            _mockCounter = new Mock<Counter>();
            _mockHistogram = new Mock<Histogram>();

            // Create the service with the correct constructor parameters
            _service = new global::TransactionService.Services.TransactionService(
                _mockRepository.Object,
                _mockRabbitMqClient.Object,
                _mockLogger.Object,
                _mockCounter.Object,
                _mockHistogram.Object);
        }

        [Fact]
        public async Task CreateTransferAsync_WithValidRequest_ReturnsTransactionResponse()
        {
            // Arrange
            var request = new TransactionRequest
            {
                FromAccount = "123456",
                ToAccount = "654321",
                Amount = 100,
                UserId = 123
            };

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                TransferId = "TRX-123456",
                UserId = request.UserId,
                FromAccount = request.FromAccount,
                ToAccount = request.ToAccount,
                Amount = request.Amount,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.CreateTransactionAsync(It.IsAny<Transaction>()))
                .ReturnsAsync(transaction);

            // Act
            var result = await _service.CreateTransferAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(transaction.TransferId, result.TransferId);
            Assert.Equal(transaction.FromAccount, result.FromAccount);
            Assert.Equal(transaction.ToAccount, result.ToAccount);
            Assert.Equal(transaction.Amount, result.Amount);
            Assert.Equal(transaction.Status, result.Status);
        }

        [Fact]
        public async Task GetTransactionByTransferIdAsync_ExistingId_ReturnsTransaction()
        {
            // Arrange
            var transferId = "TRX-123";
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                TransferId = transferId,
                UserId = 123,
                FromAccount = "123456",
                ToAccount = "654321",
                Amount = 100,
                Status = "completed",
                CreatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetTransactionByTransferIdAsync(transferId))
                .ReturnsAsync(transaction);

            // Act
            var result = await _service.GetTransactionByTransferIdAsync(transferId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(transferId, result.TransferId);
            Assert.Equal(transaction.FromAccount, result.FromAccount);
            Assert.Equal(transaction.ToAccount, result.ToAccount);
            Assert.Equal(transaction.Amount, result.Amount);
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_ExistingAccount_ReturnsTransactions()
        {
            // Arrange
            var accountId = "123456";
            var transactions = new List<Transaction>
            {
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    TransferId = "TRX-123",
                    UserId = 123,
                    FromAccount = accountId,
                    ToAccount = "654321",
                    Amount = 100,
                    Status = "completed",
                    CreatedAt = DateTime.UtcNow
                },
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    TransferId = "TRX-456",
                    UserId = 123,
                    FromAccount = "654321",
                    ToAccount = accountId,
                    Amount = 200,
                    Status = "completed",
                    CreatedAt = DateTime.UtcNow
                }
            };

            _mockRepository.Setup(r => r.GetTransactionsByAccountAsync(accountId))
                .ReturnsAsync(transactions);

            // Act
            var results = await _service.GetTransactionsByAccountAsync(accountId);

            // Assert
            Assert.NotNull(results);
            Assert.Equal(2, results.Count());
            Assert.Contains(results, r => r.TransferId == "TRX-123");
            Assert.Contains(results, r => r.TransferId == "TRX-456");
        }
    }
}