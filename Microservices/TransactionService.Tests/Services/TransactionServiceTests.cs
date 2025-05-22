using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
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
        
        public TransactionServiceTests()
        {
            _mockRepository = new Mock<ITransactionRepository>();
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
            
            var service = CreateMinimalService();

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
            
            var service = CreateMinimalService();

            // Act
            var result = await service.GetTransactionByTransferIdAsync(transferId);

            // Assert
            Assert.Null(result);
        }
        
        [Fact]
        public async Task GetTransactionsByAccountAsync_ExistingAccount_ReturnsTransactions()
        {
            // Arrange
            var accountId = "123";
            var userId = 1;
            
            var transactions = new[] {
                new Transaction {
                    Id = "1",
                    UserId = userId,
                    FromAccount = accountId,
                    ToAccount = "456",
                    Amount = 100
                }
            };
            
            _mockRepository.Setup(r => r.GetTransactionsByAccountIdAsync(accountId))
                .ReturnsAsync(transactions);
                
            var userAccountClient = new Mock<IUserAccountClient>();
            userAccountClient.Setup(c => c.GetAccountAsync(123))
                .ReturnsAsync(new Account { Id = 123, UserId = userId });
            
            var service = CreateMinimalService(userAccountClient: userAccountClient.Object);

            // Act
            var result = await service.GetTransactionsByAccountAsync(accountId, userId);

            // Assert
            Assert.Single(result);
            Assert.Equal(accountId, result[0].FromAccount);
        }
        
        // Helper to create a minimal service with only the dependencies we need
        private TransactionService.Services.TransactionService CreateMinimalService(
            IUserAccountClient userAccountClient = null)
        {
            var mockLogger = new Mock<ILogger<TransactionService.Services.TransactionService>>();
            var mockUserClient = userAccountClient ?? new Mock<IUserAccountClient>().Object;
            var mockFraudService = new Mock<IFraudDetectionService>().Object;
            var mockRabbitMq = new Mock<Infrastructure.Messaging.RabbitMQ.IRabbitMqClient>().Object;
            
            // Create counters - actual instances but won't be used in the tests we're running
            var counter1 = Prometheus.Metrics.CreateCounter("c1", "c1", new Prometheus.CounterConfiguration { LabelNames = new[] {"endpoint"} });
            var counter2 = Prometheus.Metrics.CreateCounter("c2", "c2", new Prometheus.CounterConfiguration { LabelNames = new[] {"endpoint"} });
            var counter3 = Prometheus.Metrics.CreateCounter("c3", "c3", new Prometheus.CounterConfiguration { LabelNames = new[] {"endpoint"} });
            var histogram = Prometheus.Metrics.CreateHistogram("h", "h", new Prometheus.HistogramConfiguration { LabelNames = new[] {"endpoint"} });
            
            // Create validator - real instance but won't be used in our tests
            var validatorLogger = new Mock<ILogger<TransactionValidator>>().Object;
            var httpClient = new Mock<System.Net.Http.IHttpClientFactory>().Object;
            var validatorCounter = Prometheus.Metrics.CreateCounter("vc", "vc");
            var validator = new TransactionValidator(validatorLogger, mockUserClient, httpClient, validatorCounter);
            
            return new TransactionService.Services.TransactionService(
                mockLogger.Object,
                _mockRepository.Object,
                mockUserClient,
                mockFraudService,
                validator,
                counter1,
                counter2,
                counter3,
                mockRabbitMq,
                histogram
            );
        }
    }
}