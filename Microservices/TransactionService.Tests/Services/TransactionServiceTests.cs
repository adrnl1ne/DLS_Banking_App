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
            _mockValidator = new Mock<TransactionValidator>();
            _mockRabbitMqClient = new Mock<IRabbitMqClient>();
            
            // Create real metrics with properly configured labels
            _requestsTotal = Metrics.CreateCounter("test_requests_total", "Test requests counter", new CounterConfiguration { 
                LabelNames = new[] { "action" }
            });
            
            _successesTotal = Metrics.CreateCounter("test_successes_total", "Test successes counter", new CounterConfiguration { 
                LabelNames = new[] { "action" }
            });
            
            _errorsTotal = Metrics.CreateCounter("test_errors_total", "Test errors counter", new CounterConfiguration { 
                LabelNames = new[] { "action" }
            });
            
            _histogram = Metrics.CreateHistogram("test_histogram", "Test histogram", new HistogramConfiguration {
                Buckets = new[] { 0.1, 0.5, 1, 2, 5 }
            });

            // Create the transaction service with real metrics objects
            _service = new TransactionService.Services.TransactionService(
                _mockLogger.Object,
                _mockRepository.Object,
                _mockUserAccountClient.Object,
                _mockFraudDetectionService.Object,
                _mockValidator.Object,
                _requestsTotal,
                _successesTotal,
                _errorsTotal,
                _mockRabbitMqClient.Object,
                _histogram
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

            var fromAccount = new Account { Id = 4, UserId = 1, Amount = 500m };
            var toAccount = new Account { Id = 2, UserId = 2, Amount = 200m };
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
                
            _mockValidator.Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                r.UserId == request.UserId)))
                .ReturnsAsync((fromAccount, toAccount));

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
        public async Task CreateTransferAsync_FraudDetected_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new TransactionRequest
            {
                UserId = 1,
                FromAccount = "4",
                ToAccount = "2",
                Amount = 10000m, // Large amount to trigger fraud
                Description = "Large transfer",
                TransactionType = "transfer" // Required field
            };

            var fromAccount = new Account { Id = 4, UserId = 1, Amount = 50000m };
            var toAccount = new Account { Id = 2, UserId = 2, Amount = 200m };
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
                Description = "Large transfer", // Required field
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _mockFraudDetectionService.Setup(s => s.IsServiceAvailableAsync())
                .ReturnsAsync(true);
                
            _mockValidator.Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                r.UserId == request.UserId &&
                r.Amount == request.Amount)))
                .ReturnsAsync((fromAccount, toAccount));

            _mockRepository.Setup(r => r.CreateTransactionAsync(It.IsAny<Transaction>()))
                .ReturnsAsync(transaction);

            _mockFraudDetectionService.Setup(s => s.CheckFraudAsync(It.IsAny<string>(), It.IsAny<Transaction>()))
                .ReturnsAsync(new FraudResult { 
                    IsFraud = true, 
                    Status = "rejected", 
                    TransferId = transaction.TransferId,
                    Amount = transaction.Amount,
                    Timestamp = DateTime.UtcNow
                });

            _mockRepository.Setup(r => r.GetTransactionByTransferIdAsync(It.IsAny<string>()))
                .ReturnsAsync(transaction);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateTransferAsync(request));
            
            // Verify transaction status was updated to failed
            _mockRepository.Verify(r => r.UpdateTransactionStatusAsync(It.IsAny<string>(), "declined"), Times.AtLeastOnce());
        }

        [Fact]
        public async Task CreateTransferAsync_UserNotOwnerOfFromAccount_ThrowsUnauthorizedException()
        {
            // Arrange
            var request = new TransactionRequest
            {
                UserId = 1,
                FromAccount = "4", // Account belonging to user 2
                ToAccount = "2",
                Amount = 100m,
                Description = "Test transfer",
                TransactionType = "transfer" // Required field
            };
            
            _mockFraudDetectionService.Setup(s => s.IsServiceAvailableAsync())
                .ReturnsAsync(true);
                
            _mockValidator.Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                r.FromAccount == request.FromAccount)))
                .ThrowsAsync(new UnauthorizedAccessException("You do not own this account"));

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
                Amount = 1000m, // More than account balance
                Description = "Test transfer",
                TransactionType = "transfer" // Required field
            };
            
            _mockFraudDetectionService.Setup(s => s.IsServiceAvailableAsync())
                .ReturnsAsync(true);
                
            _mockValidator.Setup(v => v.ValidateTransferRequestAsync(It.Is<TransactionRequest>(r => 
                r.Amount == request.Amount)))
                .ThrowsAsync(new InvalidOperationException("Insufficient funds"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateTransferAsync(request));
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
                Description = "Test transaction", // Required field
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
            
            // _mockRequestsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
            // _mockSuccessesTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
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
            // _mockRequestsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
            // _mockErrorsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
        }

        #endregion

        #region GetTransactionsByAccountAsync Tests

        [Fact]
        public async Task GetTransactionsByAccountAsync_ValidAccount_ReturnsTransactions()
        {
            // Arrange
            var accountId = "4";
            var userId = 1; // Owner of the account
            var transactions = new List<Transaction>
            {
                new Transaction
                {
                    Id = Guid.NewGuid().ToString(),
                    TransferId = "tx1",
                    UserId = userId,
                    FromAccount = accountId,
                    ToAccount = "2",
                    Amount = 100m,
                    Status = "completed",
                    TransactionType = "transfer",
                    Description = "Test transaction 1", // Required field
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Transaction
                {
                    Id = Guid.NewGuid().ToString(),
                    TransferId = "tx2",
                    UserId = 2,
                    FromAccount = "2",
                    ToAccount = accountId,
                    Amount = 50m,
                    Status = "completed",
                    TransactionType = "transfer",
                    Description = "Test transaction 2", // Required field
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            _mockUserAccountClient.Setup(c => c.GetAccountAsync(It.Is<int>(id => id == int.Parse(accountId))))
                .ReturnsAsync(new Account { Id = int.Parse(accountId), UserId = userId });

            _mockRepository.Setup(r => r.GetTransactionsByAccountAsync(accountId))
                .ReturnsAsync(transactions);

            // Act
            var results = await _service.GetTransactionsByAccountAsync(accountId, userId);

            // Assert
            Assert.NotNull(results);
            Assert.Equal(2, results.Count());
            Assert.Contains(results, t => t.TransferId == "tx1");
            Assert.Contains(results, t => t.TransferId == "tx2");
            
            // _mockRequestsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
            // _mockSuccessesTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_InvalidAccountIdFormat_ThrowsArgumentException()
        {
            // Arrange
            var accountId = "invalid-id"; // Not a number
            var userId = 1;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.GetTransactionsByAccountAsync(accountId, userId));
            
            // _mockRequestsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
            // _mockErrorsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_AccountNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            var accountId = "999"; // Non-existent account
            var userId = 1;

            _mockUserAccountClient.Setup(c => c.GetAccountAsync(It.Is<int>(id => id == int.Parse(accountId))))
                .ReturnsAsync((Account)null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                _service.GetTransactionsByAccountAsync(accountId, userId));
            
            // _mockRequestsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
            // _mockErrorsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
        }

        [Fact]
        public async Task GetTransactionsByAccountAsync_UserNotOwner_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var accountId = "4";
            var accountOwnerId = 2; // Real owner
            var requestingUserId = 1; // Different user

            _mockUserAccountClient.Setup(c => c.GetAccountAsync(It.Is<int>(id => id == int.Parse(accountId))))
                .ReturnsAsync(new Account { Id = int.Parse(accountId), UserId = accountOwnerId });

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
                _service.GetTransactionsByAccountAsync(accountId, requestingUserId));
            
            // _mockRequestsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
            // _mockErrorsTotal.Verify(c => c.WithLabels(It.IsAny<string>()), Times.AtLeastOnce());
        }

        #endregion
    }
}