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
    public class TransactionServiceTests
    {
        private readonly Mock<ILogger<TransactionService.Services.TransactionService>> _mockLogger;
        private readonly Mock<ITransactionRepository> _mockRepository;
        private readonly Mock<IUserAccountClient> _mockUserAccountClient;
        private readonly Mock<IFraudDetectionService> _mockFraudDetectionService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<TransactionValidator>> _mockValidatorLogger;
        private readonly Counter _validatorCounter;
        private readonly ITransactionValidator _validator; // Use interface instead
        private readonly Counter _requestsTotal;
        private readonly Counter _successesTotal;
        private readonly Counter _errorsTotal;
        private readonly Mock<IRabbitMqClient> _mockRabbitMqClient;
        private readonly Histogram _histogram;
        private readonly TransactionService.Services.TransactionService _service;

        public TransactionServiceTests()
        {
            _mockLogger = new Mock<ILogger<TransactionService.Services.TransactionService>>();
            _mockRepository = new Mock<ITransactionRepository>();
            _mockUserAccountClient = new Mock<IUserAccountClient>();
            _mockFraudDetectionService = new Mock<IFraudDetectionService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockValidatorLogger = new Mock<ILogger<TransactionValidator>>();
            _mockRabbitMqClient = new Mock<IRabbitMqClient>();
            
            // Create a mock validator instead of using the real class
            var mockValidator = new Mock<ITransactionValidator>();
            _validator = mockValidator.Object;
            
            // Create counters with labels - must match exactly what TransactionService expects
            _validatorCounter = Metrics.CreateCounter("validator_counter", "Validator counter", new CounterConfiguration {
                LabelNames = new[] { "operation" }
            });
            
            _requestsTotal = Metrics.CreateCounter("test_requests_total", "Test requests counter", new CounterConfiguration { 
                LabelNames = new[] { "operation" }
            });
            
            _successesTotal = Metrics.CreateCounter("test_successes_total", "Test successes counter", new CounterConfiguration { 
                LabelNames = new[] { "operation" }
            });
            
            _errorsTotal = Metrics.CreateCounter("test_errors_total", "Test errors counter", new CounterConfiguration { 
                LabelNames = new[] { "operation" }
            });
            
            _histogram = Metrics.CreateHistogram("test_histogram", "Test histogram", new HistogramConfiguration {
                Buckets = new[] { 0.1, 0.5, 1, 2, 5 }
            });

            // Initialize the service with the mock validator
            _service = new TransactionService.Services.TransactionService(
                _mockLogger.Object,
                _mockRepository.Object,
                _mockUserAccountClient.Object,
                _mockFraudDetectionService.Object,
                _validator,
                _requestsTotal,
                _successesTotal,
                _errorsTotal,
                _mockRabbitMqClient.Object,
                _histogram
            );

            // Set up ValidateTransferRequestAsync for different test cases
            mockValidator.Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                r.UserId == 1 && r.FromAccount == "4" && r.Amount <= 500m)))
                .ReturnsAsync((
                    new Account { Id = 4, UserId = 1, Amount = 500m },
                    new Account { Id = 2, UserId = 2, Amount = 200m }
                ));
            
            // Setup for unauthorized access test
            mockValidator.Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                r.UserId != 2 && r.FromAccount == "4" && r.Amount <= 500m && r.Description == "Unauthorized test")))
                .ThrowsAsync(new UnauthorizedAccessException("You do not own this account"));
            
            // Setup for insufficient funds test
            mockValidator.Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                r.Amount > 500m)))
                .ThrowsAsync(new InvalidOperationException("Insufficient funds"));
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
            
            // Verify repository was called
            _mockRepository.Verify(r => r.CreateTransactionAsync(It.IsAny<Transaction>()), Times.AtLeastOnce());
            // Verify fraud detection was called
            _mockFraudDetectionService.Verify(s => s.CheckFraudAsync(It.IsAny<string>(), It.IsAny<Transaction>()), Times.Once());
            // Verify RabbitMQ was called to send balance updates
            _mockRabbitMqClient.Verify(r => r.Publish(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeast(1));
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
                UserId = 1,
                FromAccount = "4", 
                ToAccount = "2",
                Amount = 100m,
                Description = "Unauthorized test", // Matches setup in constructor
                TransactionType = "transfer"
            };
            
            _mockFraudDetectionService.Setup(s => s.IsServiceAvailableAsync())
                .ReturnsAsync(true);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateTransferAsync(request));
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
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateTransferAsync(request));
        }

        // More test methods here
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