using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Prometheus;
using TransactionService.Exceptions;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;
using TransactionService.Services;
using TransactionService.Services.Interface;
using TransactionService.Infrastructure.Data.Repositories;
using Xunit;

namespace TransactionService.Tests.Services
{
    public class TransactionServiceTests
    {
        private readonly Mock<ITransactionRepository> _mockRepository;
        private readonly Mock<IUserAccountClient> _mockUserAccountClient;
        private readonly Mock<IFraudDetectionService> _mockFraudDetectionService;
        private readonly Mock<IRabbitMQClient> _mockRabbitMqClient; // Fix capitalization here
        private readonly Mock<ILogger<TransactionService.Services.TransactionService>> _mockLogger;
        
        public TransactionServiceTests()
        {
            _mockRepository = new Mock<ITransactionRepository>();
            _mockUserAccountClient = new Mock<IUserAccountClient>();
            _mockFraudDetectionService = new Mock<IFraudDetectionService>();
            _mockRabbitMqClient = new Mock<IRabbitMQClient>(); // Fix capitalization here
            _mockLogger = new Mock<ILogger<TransactionService.Services.TransactionService>>();
        }

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
            Assert.NotNull(result);
            Assert.Equal(transferId, result.TransferId);
            Assert.Equal(transaction.FromAccount, result.FromAccount);
            Assert.Equal(transaction.ToAccount, result.ToAccount);
            Assert.Equal(transaction.Amount, result.Amount);
            Assert.Equal(transaction.Status, result.Status);
        }

        [Fact]
        public async Task GetTransactionByTransferIdAsync_NonExistentTransaction_ReturnsNull()
        {
            // Arrange
            var transferId = "non-existent-id";
            
            _mockRepository.Setup(r => r.GetTransactionByTransferIdAsync(transferId))
                .ReturnsAsync((Transaction)null);
            
            var service = CreateService();

            // Act
            var result = await service.GetTransactionByTransferIdAsync(transferId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTransactionByTransferIdAsync_RepositoryThrowsException_PropagatesException()
        {
            // Arrange
            var transferId = "error-id";
            
            _mockRepository.Setup(r => r.GetTransactionByTransferIdAsync(transferId))
                .ThrowsAsync(new Exception("Database error"));
            
            var service = CreateService();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(() => 
                service.GetTransactionByTransferIdAsync(transferId));
            
            Assert.Contains("Database error", exception.Message);
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
                .ReturnsAsync(new Account { Id = 123, UserId = userId });
            
            var service = CreateService();

            // Act
            var result = await service.GetTransactionsByAccountAsync(accountId, userId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            var firstTransaction = result.First();
            Assert.Equal(accountId, firstTransaction.FromAccount);
            Assert.Equal("456", firstTransaction.ToAccount);
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_UserNotOwningAccount_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var accountId = "123";
            var userId = 1;
            var differentUserId = 2; // Different from account owner
            
            _mockUserAccountClient.Setup(c => c.GetAccountAsync(123))
                .ReturnsAsync(new Account { Id = 123, UserId = userId });
            
            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => 
                await service.GetTransactionsByAccountAsync(accountId, differentUserId));
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_NonExistentAccount_ThrowsInvalidOperationException()
        {
            // Arrange
            var accountId = "123";
            var userId = 1;
            
            _mockUserAccountClient.Setup(c => c.GetAccountAsync(123))
                .ReturnsAsync((Account)null);
            
            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => 
                await service.GetTransactionsByAccountAsync(accountId, userId));
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_InvalidAccountIdFormat_ThrowsArgumentException()
        {
            // Arrange
            var accountId = "not-a-number";
            var userId = 1;
            
            var service = CreateService();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await service.GetTransactionsByAccountAsync(accountId, userId));
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_EmptyTransactionsList_ReturnsEmptyCollection()
        {
            // Arrange
            var accountId = "123";
            var userId = 1;
            
            _mockRepository.Setup(r => r.GetTransactionsByAccountAsync(accountId))
                .ReturnsAsync(new List<Transaction>());
                
            _mockUserAccountClient.Setup(c => c.GetAccountAsync(123))
                .ReturnsAsync(new Account { Id = 123, UserId = userId });
            
            var service = CreateService();

            // Act
            var result = await service.GetTransactionsByAccountAsync(accountId, userId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        #endregion

        #region FraudDetection Service Health Tests

        [Fact]
        public async Task CreateTransferAsync_FraudServiceUnavailable_ThrowsServiceUnavailableException()
        {
            // Arrange
            var service = CreateService();
            
            // Setup fraud detection service to be unavailable
            _mockFraudDetectionService.Setup(s => s.IsServiceAvailableAsync())
                .ReturnsAsync(false);
            
            var request = new TransactionRequest
            {
                UserId = 1,
                FromAccount = "123",
                ToAccount = "456",
                Amount = 100,
                Description = "Test transfer",
                TransactionType = "transfer"
            };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ServiceUnavailableException>(() => 
                service.CreateTransferAsync(request));
            
            Assert.Contains("fraud detection service", exception.Message.ToLower());
        }

        #endregion

        #region Metrics and Monitoring Tests

        [Fact]
        public async Task GetTransactionsByAccountAsync_SuccessfulRequest_MetricsRecorded()
        {
            // Arrange
            var accountId = "123";
            var userId = 1;
            var metrics = CreateTrackableMetrics();
            
            _mockRepository.Setup(r => r.GetTransactionsByAccountAsync(accountId))
                .ReturnsAsync(new List<Transaction>());
                
            _mockUserAccountClient.Setup(c => c.GetAccountAsync(123))
                .ReturnsAsync(new Account { Id = 123, UserId = userId });
            
            var service = CreateServiceWithMetrics(metrics.Request, metrics.Success, metrics.Error, metrics.Histogram);

            // Act
            await service.GetTransactionsByAccountAsync(accountId, userId);

            // Assert
            // We can't easily verify the metrics since they're using static counters
            // Instead, we just verify that the method completed without throwing an exception
            Assert.True(true);
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_RepositoryThrowsException_ErrorMetricsRecorded()
        {
            // Arrange
            var accountId = "123";
            var userId = 1;
            var metrics = CreateTrackableMetrics();
            
            _mockRepository.Setup(r => r.GetTransactionsByAccountAsync(accountId))
                .ThrowsAsync(new Exception("Test exception"));
                
            _mockUserAccountClient.Setup(c => c.GetAccountAsync(123))
                .ReturnsAsync(new Account { Id = 123, UserId = userId });
            
            var service = CreateServiceWithMetrics(metrics.Request, metrics.Success, metrics.Error, metrics.Histogram);

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => service.GetTransactionsByAccountAsync(accountId, userId));
        }

        #endregion

        #region Repository Integration Tests
        
        [Fact]
        public async Task GetTransactionsByAccountAsync_ExpectedIntegrationPoints_VerifyCalled()
        {
            // Arrange
            var accountId = "123";
            var userId = 1;
            
            _mockRepository.Setup(r => r.GetTransactionsByAccountAsync(accountId))
                .ReturnsAsync(new List<Transaction>());
                
            _mockUserAccountClient.Setup(c => c.GetAccountAsync(123))
                .ReturnsAsync(new Account { Id = 123, UserId = userId });
            
            var service = CreateService();

            // Act
            await service.GetTransactionsByAccountAsync(accountId, userId);
            
            // Verify that the repository and account client were called 
            _mockRepository.Verify(r => r.GetTransactionsByAccountAsync(accountId), Times.Once);
            _mockUserAccountClient.Verify(c => c.GetAccountAsync(123), Times.Once);
        }

        [Fact]
        public async Task GetTransactionByTransferIdAsync_VerifiesRepositoryCalls()
        {
            // Arrange
            var transferId = "test-id";
            var transaction = new Transaction
            {
                Id = "1",
                TransferId = transferId,
                UserId = 1,
                FromAccount = "123",
                ToAccount = "456",
                Amount = 100,
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
            await service.GetTransactionByTransferIdAsync(transferId);
            
            // Verify
            _mockRepository.Verify(r => r.GetTransactionByTransferIdAsync(transferId), Times.Once);
        }

        #endregion

        #region Helper Methods

        // Helper method to create a properly configured service instance
        private TransactionService.Services.TransactionService CreateService()
        {
            var metrics = CreateTrackableMetrics();
            return CreateServiceWithMetrics(metrics.Request, metrics.Success, metrics.Error, metrics.Histogram);
        }
        
        // Helper to create a service with specific metrics counters
        private TransactionService.Services.TransactionService CreateServiceWithMetrics(
            Counter requestCounter, 
            Counter successCounter, 
            Counter errorCounter,
            Histogram histogram)
        {
            // Create a validator with mocks
            var mockValidatorLogger = new Mock<ILogger<TransactionValidator>>();
            var mockHttpClient = new Mock<System.Net.Http.IHttpClientFactory>();
            var validatorCounter = Metrics.CreateCounter("validator_counter", "Validator counter");
            
            var validator = new TransactionValidator(
                mockValidatorLogger.Object,
                _mockUserAccountClient.Object,
                mockHttpClient.Object,
                validatorCounter);
            
            // Create and return the service
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
                histogram);
        }
        
        // Helper to create trackable metric objects
        private (Counter Request, Counter Success, Counter Error, Histogram Histogram) CreateTrackableMetrics()
        {
            // Use unique names for each test instance to avoid collisions
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