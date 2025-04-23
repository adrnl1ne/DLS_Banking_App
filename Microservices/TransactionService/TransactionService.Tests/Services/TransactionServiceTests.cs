using Microsoft.Extensions.Logging;
using Moq;
using Prometheus;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Models;
using Xunit;

namespace TransactionService.Tests.Services
{
    public class TransactionServiceTests
    {
        private readonly Mock<ITransactionRepository> _mockRepository;
        private readonly global::TransactionService.Services.TransactionService _service;

        public TransactionServiceTests()
        {
            _mockRepository = new Mock<ITransactionRepository>();
            var mock = new Mock<IRabbitMqClient>();
            if (mock == null) throw new ArgumentNullException(nameof(mock));
            Mock<ILogger<TransactionService.Services.TransactionService>> mockLogger = new();
            var mock1 = new Mock<Counter>();
            if (mock1 == null) throw new ArgumentNullException(nameof(mock1));
            Mock<Histogram> mockHistogram = new();

            // Create the service with the correct constructor parameters
            _service = new global::TransactionService.Services.TransactionService(
                _mockRepository.Object,
                mockLogger.Object,
                mockHistogram.Object);
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
            const string accountId = "123456";
            List<Transaction> transactions =
            [
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
            ];

            _mockRepository.Setup(r => r.GetTransactionsByAccountAsync(accountId))
                .ReturnsAsync(transactions);

            // Act
            var results = await _service.GetTransactionsByAccountAsync(accountId);

            // Assert
            Assert.NotNull(results);
            var transactionResponses = results as TransactionResponse?[] ?? results.ToArray();
            Assert.Equal(2, transactionResponses.Length);
            Assert.Contains(transactionResponses, r => r?.TransferId == "TRX-123");
            Assert.Contains(transactionResponses, r => r?.TransferId == "TRX-456");
        }
    }
}