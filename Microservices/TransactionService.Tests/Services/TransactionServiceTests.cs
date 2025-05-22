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
        private readonly Mock<ILogger<TransactionService.Services.TransactionService>> _mockLogger;
        private readonly Mock<ITransactionRepository> _mockRepository;
        private readonly Mock<IUserAccountClient> _mockUserAccountClient;
        private readonly Mock<IFraudDetectionService> _mockFraudDetectionService;
        private readonly Mock<TransactionValidator> _mockValidator;
        private readonly Mock<IRabbitMqClient> _mockRabbitMqClient;
        private readonly TransactionService.Services.TransactionService _service;
        
        public TransactionServiceTests()
        {
            // Initialize mocks
            _mockLogger = new Mock<ILogger<TransactionService.Services.TransactionService>>();
            _mockRepository = new Mock<ITransactionRepository>();
            _mockUserAccountClient = new Mock<IUserAccountClient>();
            _mockFraudDetectionService = new Mock<IFraudDetectionService>();
            _mockValidator = new Mock<TransactionValidator>(
                MockBehavior.Default,
                new Mock<ILogger<TransactionValidator>>().Object,
                new Mock<IUserAccountClient>().Object,
                new Mock<IHttpClientFactory>().Object,
                Metrics.CreateCounter("test_counter", "Test counter", new CounterConfiguration { LabelNames = new[] { "operation" } })
            );
            _mockRabbitMqClient = new Mock<IRabbitMqClient>();
            
            // Create counters with endpoint label
            var requestCounter = Metrics.CreateCounter("test_requests", "Test requests", new CounterConfiguration { 
                LabelNames = new[] { "endpoint" } 
            });
            var successCounter = Metrics.CreateCounter("test_success", "Test success", new CounterConfiguration { 
                LabelNames = new[] { "endpoint" } 
            });
            var errorCounter = Metrics.CreateCounter("test_errors", "Test errors", new CounterConfiguration { 
                LabelNames = new[] { "endpoint" } 
            });
            var latencyHistogram = Metrics.CreateHistogram("test_latency", "Test latency", new HistogramConfiguration {
                LabelNames = new[] { "endpoint" }
            });

            // Create service with mocks
            _service = new TransactionService.Services.TransactionService(
                _mockLogger.Object,
                _mockRepository.Object,
                _mockUserAccountClient.Object,
                _mockFraudDetectionService.Object,
                _mockValidator.Object,
                requestCounter,
                successCounter,
                errorCounter,
                _mockRabbitMqClient.Object,
                latencyHistogram
            );
            
            // Setup basic validator behavior for all tests
            SetupDefaultValidation();
        }
        
        private void SetupDefaultValidation() 
        {
            // Base setup for a valid transfer - used in success case
            _mockValidator
                .Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                    r.UserId == 1 && r.Amount <= 500)))
                .ReturnsAsync((
                    new Account { Id = 4, UserId = 1, Amount = 500m },
                    new Account { Id = 2, UserId = 2, Amount = 200m }
                ));
                
            // Setup for unauthorized access
            _mockValidator
                .Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                    r.UserId == 2 && r.FromAccount == "4")))
                .ThrowsAsync(new UnauthorizedAccessException("User does not own the account"));
                
            // Setup for insufficient funds
            _mockValidator
                .Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                    r.Amount > 500)))
                .ThrowsAsync(new InvalidOperationException("Insufficient funds"));
        }

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

            var expectedTransaction = new Transaction
            {
                Id = "test-id",
                TransferId = "test-transfer-id",
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
                .ReturnsAsync(expectedTransaction);

            _mockFraudDetectionService.Setup(s => s.CheckFraudAsync(It.IsAny<string>(), It.IsAny<Transaction>()))
                .ReturnsAsync(new FraudResult { 
                    IsFraud = false, 
                    Status = "approved",
                    TransferId = expectedTransaction.TransferId,
                    Amount = expectedTransaction.Amount,
                    Timestamp = DateTime.UtcNow
                });

            // Act
            var result = await _service.CreateTransferAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedTransaction.TransferId, result.TransferId);
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
        }

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
    }
}