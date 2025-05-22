using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    /// <summary>
    /// Simple test double for TransactionValidator
    /// </summary>
    public class TestValidator : TransactionValidator
    {
        public TestValidator() : base(
            new Mock<ILogger<TransactionValidator>>().Object,
            new Mock<IUserAccountClient>().Object,
            new Mock<IHttpClientFactory>().Object,
            Metrics.CreateCounter("test_counter", "Test counter"))
        {
        }

        // Store the standard behavior for the validator
        private Func<TransactionRequest, Task<(Account, Account)>> _validateBehavior;

        // Override with test specific validation
        public void SetupValidation(Func<TransactionRequest, Task<(Account, Account)>> validateBehavior)
        {
            _validateBehavior = validateBehavior;
        }

        // Custom implementation that bypasses the base class
        public new Task<(Account, Account)> ValidateTransferRequestAsync(TransactionRequest request)
        {
            return _validateBehavior(request);
        }
    }

    public class TransactionServiceTests
    {
        private readonly Mock<ILogger<TransactionService.Services.TransactionService>> _mockLogger;
        private readonly Mock<ITransactionRepository> _mockRepository;
        private readonly Mock<IUserAccountClient> _mockUserAccountClient;
        private readonly Mock<IFraudDetectionService> _mockFraudDetectionService;
        private readonly TestValidator _validator;
        private readonly Mock<IRabbitMqClient> _mockRabbitMqClient;
        private readonly TransactionService.Services.TransactionService _service;

        public TransactionServiceTests()
        {
            _mockLogger = new Mock<ILogger<TransactionService.Services.TransactionService>>();
            _mockRepository = new Mock<ITransactionRepository>();
            _mockUserAccountClient = new Mock<IUserAccountClient>();
            _mockFraudDetectionService = new Mock<IFraudDetectionService>();
            _mockRabbitMqClient = new Mock<IRabbitMqClient>();
            
            // Create test validator
            _validator = new TestValidator();
            
            // Configure the validator's default behavior
            _validator.SetupValidation(request => {
                // Logic based on the test request
                if (request.UserId != 1 && request.FromAccount == "4")
                {
                    throw new UnauthorizedAccessException("User does not own the account");
                }

                if (request.Amount > 500)
                {
                    throw new InvalidOperationException("Insufficient funds");
                }

                return Task.FromResult((
                    new Account { Id = 4, UserId = 1, Amount = 500m },
                    new Account { Id = 2, UserId = 2, Amount = 200m }
                ));
            });
            
            // Create counters with correct label names
            var requestsCounter = Metrics.CreateCounter("requests_total", "Total requests", new CounterConfiguration {
                LabelNames = new[] { "endpoint" }
            });
            
            var successCounter = Metrics.CreateCounter("success_total", "Success requests", new CounterConfiguration {
                LabelNames = new[] { "endpoint" }
            });
            
            var errorCounter = Metrics.CreateCounter("error_total", "Error requests", new CounterConfiguration {
                LabelNames = new[] { "endpoint" }
            });
            
            var latencyHistogram = Metrics.CreateHistogram("request_duration_seconds", "Request duration in seconds", new HistogramConfiguration {
                LabelNames = new[] { "endpoint" },
                Buckets = new[] { 0.1, 0.2, 0.5, 1, 2, 5, 10 }
            });

            // Initialize the service with test components
            _service = new TransactionService.Services.TransactionService(
                _mockLogger.Object,
                _mockRepository.Object,
                _mockUserAccountClient.Object,
                _mockFraudDetectionService.Object,
                _validator,
                requestsCounter,
                successCounter,
                errorCounter,
                _mockRabbitMqClient.Object,
                latencyHistogram
            );
        }

        #region CreateTransferAsync Tests

        [Fact]
        public async Task CreateTransferAsync_ValidRequest_ReturnsSuccessfulTransaction()
        {
            // Arrange
            var request = new TransactionRequest
            {
                UserId = 1,
                FromAccount = "4",
                ToAccount = "2",
                Amount = 100m,
                Description = "Test transfer",
                TransactionType = "transfer"
            };

            var transaction = new Transaction
            {
                Id = Guid.NewGuid().ToString(),
                TransferId = Guid.NewGuid().ToString(),
                UserId = request.UserId,
                FromAccount = request.FromAccount,
                ToAccount = request.ToAccount,
                Amount = request.Amount,
                Status = "pending",
                TransactionType = "transfer",
                Description = request.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockFraudDetectionService.Setup(s => s.IsServiceAvailableAsync())
                .ReturnsAsync(true);

            _mockRepository.Setup(r => r.CreateTransactionAsync(It.IsAny<Transaction>()))
                .ReturnsAsync(transaction);

            _mockFraudDetectionService.Setup(s => s.CheckFraudAsync(It.IsAny<string>(), It.IsAny<Transaction>()))
                .ReturnsAsync(new FraudResult { 
                    IsFraud = false, 
                    Status = "approved",
                    TransferId = transaction.TransferId,
                    Amount = transaction.Amount,
                    Timestamp = DateTime.UtcNow
                });

            // Act
            var result = await _service.CreateTransferAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(transaction.TransferId, result.TransferId);
            Assert.Equal(request.FromAccount, result.FromAccount);
            Assert.Equal(request.ToAccount, result.ToAccount);
            Assert.Equal(request.Amount, result.Amount);
        }

        [Fact]
        public async Task CreateTransferAsync_FraudDetectionUnavailable_ThrowsServiceUnavailableException()
        {
            // Arrange
            var request = new TransactionRequest
            {
                UserId = 1,
                FromAccount = "4",
                ToAccount = "2",
                Amount = 100m,
                Description = "Test transfer",
                TransactionType = "transfer"
            };

            _mockFraudDetectionService.Setup(s => s.IsServiceAvailableAsync())
                .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<ServiceUnavailableException>(() => _service.CreateTransferAsync(request));
        }

        [Fact]
        public async Task CreateTransferAsync_UserNotOwnerOfFromAccount_ThrowsUnauthorizedException()
        {
            // Arrange
            var request = new TransactionRequest
            {
                UserId = 2, // Different from account owner (1)
                FromAccount = "4", 
                ToAccount = "2",
                Amount = 100m,
                Description = "Test transfer",
                TransactionType = "transfer"
            };
            
            _mockFraudDetectionService.Setup(s => s.IsServiceAvailableAsync())
                .ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
                _service.CreateTransferAsync(request));
            
            Assert.Contains("User does not own the account", exception.Message);
        }

        [Fact]
        public async Task CreateTransferAsync_InsufficientFunds_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new TransactionRequest
            {
                UserId = 1,
                FromAccount = "4",
                ToAccount = "2",
                Amount = 1000m, // More than account balance (500m)
                Description = "Test transfer",
                TransactionType = "transfer"
            };
            
            _mockFraudDetectionService.Setup(s => s.IsServiceAvailableAsync())
                .ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.CreateTransferAsync(request));
                
            Assert.Contains("Insufficient funds", exception.Message);
        }

        #endregion

        #region GetTransactionByTransferIdAsync Tests

        [Fact]
        public async Task GetTransactionByTransferIdAsync_ExistingTransaction_ReturnsTransaction()
        {
            // Arrange
            var transferId = "test-transfer-id";
            var transaction = new Transaction
            {
                Id = Guid.NewGuid().ToString(),
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

            // Act
            var result = await _service.GetTransactionByTransferIdAsync(transferId);

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

            // Act
            var result = await _service.GetTransactionByTransferIdAsync(transferId);

            // Assert
            Assert.Null(result);
        }
        #endregion
    }
}