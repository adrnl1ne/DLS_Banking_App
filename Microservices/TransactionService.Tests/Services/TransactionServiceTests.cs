using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Prometheus;
using TransactionService.Exceptions;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Infrastructure.Redis;
using TransactionService.Models;
using TransactionService.Services;
using TransactionService.Services.Interface;
using TransactionService.Infrastructure.Data.Repositories;
using Xunit;
using FluentAssertions;

namespace TransactionService.Tests.Services
{
    public class TransactionServiceTests
    {
        private readonly Mock<ITransactionRepository> _mockRepository = new();
        private readonly Mock<IUserAccountClient> _mockUserAccountClient = new();
        private readonly Mock<IFraudDetectionService> _mockFraudDetectionService = new();
        private readonly Mock<IRabbitMqClient> _mockRabbitMqClient = new();
        private readonly Mock<ILogger<TransactionService.Services.TransactionService>> _mockLogger = new();
        private readonly Mock<IRedisClient> _mockRedisClient = new();
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();

        #region GetTransactionByTransferIdAsync Tests

        [Fact]
        public async Task GetTransactionByTransferIdAsync_ExistingTransaction_ReturnsTransaction()
        {
            // Arrange
            var transferId = "test-transfer-id";
            var transaction = new Transaction
            {
                Id = "test-id",
                TransferId = transferId,
                UserId = 1,
                FromAccount = "4",
                ToAccount = "2",
                Amount = 100m,
                Status = "completed",
                TransactionType = "transfer",
                Description = "Test transaction",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetTransactionByTransferIdAsync(transferId))
                .ReturnsAsync(transaction);

            var service = CreateService();

            // Act
            var result = await service.GetTransactionByTransferIdAsync(transferId);

            // Assert
            result.Should().NotBeNull();
            result!.TransferId.Should().Be(transferId);
            result.FromAccount.Should().Be(transaction.FromAccount);
            result.ToAccount.Should().Be(transaction.ToAccount);
            result.Amount.Should().Be(transaction.Amount);
            result.Status.Should().Be(transaction.Status);
            result.UserId.Should().Be(transaction.UserId);
            
            _mockRepository.Verify(r => r.GetTransactionByTransferIdAsync(transferId), Times.Once);
        }

        [Fact]
        public async Task GetTransactionByTransferIdAsync_NonExistentTransaction_ReturnsNull()
        {
            // Arrange
            var transferId = "non-existent-id";
            
            _mockRepository.Setup(r => r.GetTransactionByTransferIdAsync(transferId))
                .ReturnsAsync((Transaction?)null);
            
            var service = CreateService();

            // Act
            var result = await service.GetTransactionByTransferIdAsync(transferId);

            // Assert
            result.Should().BeNull();
            _mockRepository.Verify(r => r.GetTransactionByTransferIdAsync(transferId), Times.Once);
        }

        #endregion

        #region GetTransactionsByAccountAsync Tests

        [Fact]
        public async Task GetTransactionsByAccountAsync_ExistingAccount_ReturnsTransactions()
        {
            // Arrange
            var accountId = "123";
            var userId = 1;
            
            var transactions = new List<Transaction> {
                new Transaction {
                    Id = "1",
                    TransferId = "transfer-1",
                    UserId = userId,
                    FromAccount = accountId,
                    ToAccount = "456",
                    Amount = 100,
                    Status = "completed",
                    Description = "Test transaction 1",
                    TransactionType = "transfer",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };
            
            _mockRepository.Setup(r => r.GetTransactionsByAccountAsync(accountId))
                .ReturnsAsync(transactions);
                
            _mockUserAccountClient.Setup(c => c.GetAccountAsync(123))
                .ReturnsAsync(new Account { Id = 123, UserId = userId, Amount = 1000 }); // Using Amount not Balance
            
            var service = CreateService();

            // Act
            var result = await service.GetTransactionsByAccountAsync(accountId, userId);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            var firstTransaction = result.First();
            firstTransaction.FromAccount.Should().Be(accountId);
            firstTransaction.ToAccount.Should().Be("456");
            
            _mockRepository.Verify(r => r.GetTransactionsByAccountAsync(accountId), Times.Once);
            _mockUserAccountClient.Verify(c => c.GetAccountAsync(123), Times.Once);
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_UserNotOwningAccount_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var accountId = "123";
            var userId = 1;
            var differentUserId = 2;
            
            _mockUserAccountClient.Setup(c => c.GetAccountAsync(123))
                .ReturnsAsync(new Account { Id = 123, UserId = userId, Amount = 1000 });
            
            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => 
                await service.GetTransactionsByAccountAsync(accountId, differentUserId));
                
            _mockUserAccountClient.Verify(c => c.GetAccountAsync(123), Times.Once);
            _mockRepository.Verify(r => r.GetTransactionsByAccountAsync(It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region CreateTransferAsync Tests

        [Fact]
        public async Task CreateTransferAsync_ValidRequest_CreatesTransactionAndPublishesEvents()
        {
            // Arrange
            var request = new TransactionRequest
            {
                UserId = 1,
                FromAccount = "123",
                ToAccount = "456",
                Amount = 100,
                Description = "Test transfer"
            };

            var fromAccount = new Account { Id = 123, UserId = 1, Amount = 500 };
            var toAccount = new Account { Id = 456, UserId = 2, Amount = 200 };

            // Setup mocks
            _mockFraudDetectionService.Setup(f => f.IsServiceAvailableAsync()).ReturnsAsync(true);
            _mockFraudDetectionService.Setup(f => f.CheckFraudAsync(It.IsAny<string>(), It.IsAny<Transaction>()))
                .ReturnsAsync(new FraudResult { TransferId = "test-id", IsFraud = false, Status = "approved", Amount = 100, Timestamp = DateTime.UtcNow });

            _mockUserAccountClient.Setup(u => u.GetAccountAsync(123)).ReturnsAsync(fromAccount);
            _mockUserAccountClient.Setup(u => u.GetAccountAsync(456)).ReturnsAsync(toAccount);

            _mockRepository.Setup(r => r.CreateTransactionAsync(It.IsAny<Transaction>()))
                .ReturnsAsync((Transaction t) => t);
            _mockRepository.Setup(r => r.UpdateTransactionAsync(It.IsAny<Transaction>()))
                .ReturnsAsync((Transaction t) => t);
            _mockRepository.Setup(r => r.UpdateTransactionStatusAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string id, string status) => new Transaction 
                { 
                    Id = id, 
                    Status = status, 
                    TransferId = "test-id",
                    UserId = 1,
                    FromAccount = "123",
                    ToAccount = "456",
                    Amount = 100,
                    Description = "Test",
                    TransactionType = "transfer",
                    CreatedAt = DateTime.UtcNow
                });

            _mockRedisClient.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
            _mockRedisClient.Setup(r => r.HashSetAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .Returns(Task.CompletedTask);
            _mockRedisClient.Setup(r => r.ExpireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Returns(Task.CompletedTask);

            var service = CreateService();

            // Act
            var result = await service.CreateTransferAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("pending");
            
            _mockFraudDetectionService.Verify(f => f.IsServiceAvailableAsync(), Times.Once);
            _mockFraudDetectionService.Verify(f => f.CheckFraudAsync(It.IsAny<string>(), It.IsAny<Transaction>()), Times.Once);
            _mockRepository.Verify(r => r.CreateTransactionAsync(It.IsAny<Transaction>()), Times.Exactly(3));
            _mockRabbitMqClient.Verify(r => r.Publish("TransactionCreated", It.IsAny<string>()), Times.Once);
            _mockRedisClient.Verify(r => r.HashSetAsync(It.Is<string>(key => key.Contains("transaction:tracking")), 
                It.IsAny<Dictionary<string, string>>()), Times.Once);
            _mockRedisClient.Verify(r => r.ExpireAsync(It.Is<string>(key => key.Contains("transaction:tracking")), 
                It.IsAny<TimeSpan>()), Times.Once);
        }



        #endregion

        #region ProcessFraudResultAsync Tests


        [Fact]
        public async Task ProcessFraudResultAsync_FraudCheckPassed_QueuesBalanceUpdates()
        {
            // Arrange
            var fraudResult = new FraudResult
            {
                TransferId = "test-transfer-id",
                IsFraud = false,
                Status = "approved",
                Amount = 500,
                Timestamp = DateTime.UtcNow
            };

            var transaction = new Transaction
            {
                Id = "main-id",
                TransferId = "test-transfer-id",
                UserId = 1,
                FromAccount = "123",
                ToAccount = "456",
                Amount = 500,
                Status = "pending",
                TransactionType = "transfer",
                Description = "Test",
                CreatedAt = DateTime.UtcNow
            };

            _mockRepository.Setup(r => r.GetTransactionByTransferIdAsync("test-transfer-id"))
                .ReturnsAsync(transaction);
            _mockRepository.Setup(r => r.GetTransactionByTransferIdAsync("test-transfer-id-withdrawal"))
                .ReturnsAsync(new Transaction 
                { 
                    Id = "withdrawal-id", 
                    Status = "pending",
                    TransferId = "test-transfer-id-withdrawal",
                    UserId = 1,
                    FromAccount = "123",
                    ToAccount = "456",
                    Amount = 500,
                    TransactionType = "withdrawal",
                    Description = "Test",
                    CreatedAt = DateTime.UtcNow
                });
            _mockRepository.Setup(r => r.GetTransactionByTransferIdAsync("test-transfer-id-deposit"))
                .ReturnsAsync(new Transaction 
                { 
                    Id = "deposit-id", 
                    Status = "pending",
                    TransferId = "test-transfer-id-deposit",
                    UserId = 1,
                    FromAccount = "123",
                    ToAccount = "456",
                    Amount = 500,
                    TransactionType = "deposit",
                    Description = "Test",
                    CreatedAt = DateTime.UtcNow
                });
            _mockRepository.Setup(r => r.UpdateTransactionAsync(It.IsAny<Transaction>()))
                .ReturnsAsync((Transaction t) => t);
            _mockRepository.Setup(r => r.UpdateTransactionStatusAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string id, string status) => new Transaction 
                { 
                    Id = id, 
                    Status = status,
                    TransferId = "test-id",
                    UserId = 1,
                    FromAccount = "123",
                    ToAccount = "456",
                    Amount = 500,
                    TransactionType = "transfer",
                    Description = "Test",
                    CreatedAt = DateTime.UtcNow
                });

            var service = CreateService();

            // Act
            await service.ProcessFraudResultAsync(fraudResult);

            // Assert
            _mockRepository.Verify(r => r.UpdateTransactionAsync(It.Is<Transaction>(t => 
                t.FraudCheckResult == "verified")), Times.Once);
            _mockRepository.Verify(r => r.UpdateTransactionStatusAsync("main-id", "processing"), Times.Once);
            _mockRabbitMqClient.Verify(r => r.Publish("AccountBalanceUpdates", It.IsAny<string>()), Times.Exactly(2));
        }

        #endregion

        #region Validation Tests

        [Fact]
        public async Task CreateTransferAsync_SameAccount_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new TransactionRequest
            {
                UserId = 1,
                FromAccount = "123",
                ToAccount = "123", // Same account
                Amount = 100,
                Description = "Test transfer"
            };

            var account = new Account { Id = 123, UserId = 1, Amount = 500 };

            _mockUserAccountClient.Setup(u => u.GetAccountAsync(123)).ReturnsAsync(account);

            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await service.CreateTransferAsync(request));
        }

        [Fact]
        public async Task CreateTransferAsync_NegativeAmount_ThrowsArgumentException()
        {
            // Arrange
            var request = new TransactionRequest
            {
                UserId = 1,
                FromAccount = "123",
                ToAccount = "456",
                Amount = -100, // This should fail validation
                Description = "Test transfer"
            };

            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await service.CreateTransferAsync(request));
        }

        #endregion

        #region Helper Methods

        private TransactionService.Services.TransactionService CreateService()
        {
            var metrics = CreateTrackableMetrics();
            return CreateServiceWithMetrics(metrics.Request, metrics.Success, metrics.Error, metrics.Histogram);
        }
        
        private TransactionService.Services.TransactionService CreateServiceWithMetrics(
            Counter requestCounter, 
            Counter successCounter, 
            Counter errorCounter,
            Histogram histogram)
        {
            // Create a validator with the CORRECT constructor parameters
            var mockValidatorLogger = new Mock<ILogger<TransactionValidator>>();
            var validatorCounter = Metrics.CreateCounter($"validator_counter_{Guid.NewGuid().ToString().Substring(0, 8)}", "Validator counter");
            
            var validator = new TransactionValidator(
                mockValidatorLogger.Object,
                _mockUserAccountClient.Object,
                validatorCounter); // This is the correct third parameter, not IHttpClientFactory
            
            // Create and return the service with all required dependencies
            return new TransactionService.Services.TransactionService(
                _mockLogger.Object,
                _mockRepository.Object,
                _mockUserAccountClient.Object,
                _mockFraudDetectionService.Object,
                validator,
                requestCounter,
                successCounter,
                errorCounter,
                _mockRabbitMqClient.Object,
                histogram,
                _mockRedisClient.Object,
                _mockHttpClientFactory.Object);
        }
        
        private (Counter Request, Counter Success, Counter Error, Histogram Histogram) CreateTrackableMetrics()
        {
            var uniqueId = Guid.NewGuid().ToString().Substring(0, 8);
            
            return (
                Request: Metrics.CreateCounter($"test_requests_{uniqueId}", "Test requests", 
                    new CounterConfiguration { LabelNames = new[] { "endpoint" } }),
                Success: Metrics.CreateCounter($"test_success_{uniqueId}", "Test success",
                    new CounterConfiguration { LabelNames = new[] { "endpoint" } }),
                Error: Metrics.CreateCounter($"test_errors_{uniqueId}", "Test errors",
                    new CounterConfiguration { LabelNames = new[] { "endpoint" } }),
                Histogram: Metrics.CreateHistogram($"test_histogram_{uniqueId}", "Test histogram",
                    new HistogramConfiguration { LabelNames = new[] { "endpoint" } })
            );
        }
        
        #endregion
    }
}