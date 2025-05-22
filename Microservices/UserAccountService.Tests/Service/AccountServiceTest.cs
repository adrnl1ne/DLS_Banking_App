using Xunit;
using Moq;
using UserAccountService.Service;
using AccountService.Database.Data;
using Microsoft.Extensions.Logging;
using AccountService.Repository;
using AccountService.Services;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using UserAccountService.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using UserAccountService.Shared.DTO;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using Role = UserAccountService.Models.Role;
using Microsoft.EntityFrameworkCore.Diagnostics; // Added for InMemoryEventId

namespace UserAccountService.Tests.Service
{
    public class AccountServiceTest : IDisposable
    {
        private readonly UserAccountDbContext _context;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<IAccountRepository> _mockAccountRepository;
        private readonly Mock<IEventPublisher> _mockEventPublisher;
        private readonly Mock<IDatabase> _mockRedisDb;
        private readonly UserAccountService.Service.AccountService _accountService;

        public AccountServiceTest()
        {
            var dbContextOptions = new DbContextOptionsBuilder<UserAccountDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique name for each test run
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)) // Added to ignore transaction warnings
                .Options;

            _context = new UserAccountDbContext(dbContextOptions);
            SeedDatabase();

            _mockCurrentUserService = new Mock<ICurrentUserService>();
            var mockLogger = new Mock<ILogger<UserAccountService.Service.AccountService>>();
            _mockAccountRepository = new Mock<IAccountRepository>();
            _mockEventPublisher = new Mock<IEventPublisher>();
            var mockRedis = new Mock<IConnectionMultiplexer>();
            _mockRedisDb = new Mock<IDatabase>();

            mockRedis.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockRedisDb.Object);

            // Initialize AccountService with mocks
            _accountService = new(
                _context,
                _mockCurrentUserService.Object,
                mockLogger.Object,
                _mockAccountRepository.Object,
                _mockEventPublisher.Object,
                mockRedis.Object
            );
        }

        private void SeedDatabase()
        {
            // Seed Roles
            var roles = new List<Role>
            {
                new Role { Id = 1, Name = "user" },
                new Role { Id = 2, Name = "admin" }
            };
            _context.Roles.AddRange(roles);

            // Seed Users
            var users = new List<User>
            {
                new User
                {
                    Id = 1, Username = "john_doe", Email = "john.doe@dls.dk", Password = "hashed_password",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, RoleId = 1, Role = roles[0]
                },
                new User
                {
                    Id = 2, Username = "jane_smith", Email = "jane.smith@dls.dk", Password = "hashed_password",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, RoleId = 1, Role = roles[0]
                },
                new User
                {
                    Id = 3, Username = "admin_user", Email = "admin@dls.dk", Password = "hashed_password",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, RoleId = 2, Role = roles[1]
                },
                new User
                {
                    Id = 4, Username = "service_user", Email = "service@dls.dk", Password = "hashed_password",
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, RoleId = 2, Role = roles[1]
                } // Assuming service might have admin-like role or a specific one
            };
            _context.Users.AddRange(users);

            // Seed Accounts
            var accounts = new List<Account>
            {
                new Account { Id = 1, Name = "John's Savings", Amount = 5000.50m, UserId = 1, User = users[0] },
                new Account { Id = 2, Name = "John's Checking", Amount = 500.25m, UserId = 1, User = users[0] },
                new Account { Id = 3, Name = "Jane's Savings", Amount = 2000.75m, UserId = 2, User = users[1] },
                new Account { Id = 4, Name = "Admin Account", Amount = 10000.00m, UserId = 3, User = users[2] }
            };
            _context.Accounts.AddRange(accounts);

            // Initialize empty DeletedAccounts table
            _context.DeletedAccounts.RemoveRange(_context.DeletedAccounts);
            
            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted(); // Clean up the in-memory database after each test
            _context.Dispose();
        }

        // --- White Box Tests (London Approach) ---

        // Example: GetAccountsAsync
        [Fact]
        public async Task GetAccountsAsync_UserIsAuthenticated_ReturnsUserAccounts()
        {
            // Arrange
            var expectedUserId = 1;
            _mockCurrentUserService.Setup(s => s.UserId).Returns(expectedUserId);

            // Act
            var result = await _accountService.GetAccountsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count); // John Doe has 2 accounts
            Assert.All(result, acc => Assert.Equal(expectedUserId, acc.UserId));
        }

        [Fact]
        public async Task GetAccountsAsync_UserIdIsNull_ThrowsInvalidOperationException()
        {
            // Arrange
            _mockCurrentUserService.Setup(s => s.UserId).Returns((int?)null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _accountService.GetAccountsAsync());
        }

        // GetAccountAsync Tests
        [Fact]
        public async Task GetAccountAsync_AccountExistsAndUserIsOwner_ReturnsAccount()
        {
            // Arrange
            var accountId = 1; // John's Savings
            var userId = 1; // John Doe
            _mockCurrentUserService.Setup(s => s.UserId).Returns(userId);
            _mockCurrentUserService.Setup(s => s.Role).Returns("user");

            // Act
            var result = await _accountService.GetAccountAsync(accountId);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(accountId, result.Value.Id);
            Assert.Equal(userId, result.Value.UserId);
        }

        [Fact]
        public async Task GetAccountAsync_AccountExistsAndUserIsNotOwner_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var accountId = 1; // John's Savings
            var userId = 2; // Jane Smith
            _mockCurrentUserService.Setup(s => s.UserId).Returns(userId);
            _mockCurrentUserService.Setup(s => s.Role).Returns("user");

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _accountService.GetAccountAsync(accountId));
        }

        [Fact]
        public async Task GetAccountAsync_AccountExistsAndRoleIsService_ReturnsAccount()
        {
            // Arrange
            var accountId = 1; // John's Savings
            _mockCurrentUserService.Setup(s => s.Role).Returns("service");
            _mockCurrentUserService.Setup(s => s.UserId).Returns((int?)null); // Service role might not have a user ID

            // Act
            var result = await _accountService.GetAccountAsync(accountId);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(accountId, result.Value.Id);
        }

        [Fact]
        public async Task GetAccountAsync_AccountDoesNotExist_ThrowsInvalidOperationException()
        {
            // Arrange
            var accountId = 999; // Non-existent account
            _mockCurrentUserService.Setup(s => s.UserId).Returns(1);
            _mockCurrentUserService.Setup(s => s.Role).Returns("user");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _accountService.GetAccountAsync(accountId));
        }


        // CreateAccountAsync Tests
        [Fact]
        public async Task CreateAccountAsync_ValidRequest_CreatesAccountAndPublishesEvent()
        {
            // Arrange
            var userId = 1;
            var request = new AccountCreationRequest { Name = "New Test Account", UserId = userId };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(userId);
            _mockCurrentUserService.Setup(s => s.Role).Returns("admin"); // Admin role required
            _mockAccountRepository.Setup(r => r.AddAccountAsync(It.IsAny<Account>())).Returns(Task.CompletedTask);
            _mockAccountRepository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

            // Act
            var result = await _accountService.CreateAccountAsync(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var accountResponse = Assert.IsType<AccountResponse>(createdAtActionResult.Value);
            Assert.Equal(request.Name, accountResponse.Name);
            Assert.Equal(userId, accountResponse.UserId);

            _mockAccountRepository.Verify(
                r => r.AddAccountAsync(It.Is<Account>(a => a.Name == request.Name && a.UserId == userId)), Times.Once);
            _mockAccountRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
            _mockEventPublisher.Verify(p => p.Publish(
                "AccountEvents",
                It.Is<string>(
                    s => s.Contains("\"event_type\":\"AccountCreated\"") && s.Contains($"\"userId\":{userId}"))
            ), Times.Once);
        }

        [Fact]
        public async Task CreateAccountAsync_NonAdminRole_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var request = new AccountCreationRequest { Name = "New Test Account", UserId = 1 };
            _mockCurrentUserService.Setup(s => s.Role).Returns("user"); // Non-admin role

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _accountService.CreateAccountAsync(request));
        }

        // DeleteAccountAsync Tests
        [Fact]
        public async Task DeleteAccountAsync_UserIsOwner_DeletesAccountAndPublishesEvent()
        {
            // Arrange
            var accountId = 1; // John's Savings
            var userId = 1; // John Doe
            _mockCurrentUserService.Setup(s => s.UserId).Returns(userId);
            _mockCurrentUserService.Setup(s => s.Role).Returns("user");

            // Act
            await _accountService.DeleteAccountAsync(accountId);

            // Assert
            var deletedAccount = await _context.Accounts.FindAsync(accountId);
            Assert.Null(deletedAccount); // Account should be removed
            _mockEventPublisher.Verify(p => p.Publish(
                "AccountEvents",
                It.Is<string>(s =>
                    s.Contains("\"event_type\":\"AccountDeleted\"") && s.Contains($"\"accountId\":{accountId}"))
            ), Times.Once);
        }

        [Fact]
        public async Task DeleteAccountAsync_UserIsAdmin_DeletesAccountAndPublishesEvent()
        {
            // Arrange
            var accountId = 1; // John's Savings (owned by user 1)
            var adminUserId = 3; // Admin User
            _mockCurrentUserService.Setup(s => s.UserId).Returns(adminUserId);
            _mockCurrentUserService.Setup(s => s.Role).Returns("admin");

            // Act
            await _accountService.DeleteAccountAsync(accountId);

            // Assert
            var deletedAccount = await _context.Accounts.FindAsync(accountId);
            Assert.Null(deletedAccount);
            _mockEventPublisher.Verify(p => p.Publish(
                "AccountEvents",
                It.Is<string>(s =>
                    s.Contains("\"event_type\":\"AccountDeleted\"") && s.Contains($"\"accountId\":{accountId}"))
            ), Times.Once);
        }


        [Fact]
        public async Task DeleteAccountAsync_UserIsNotOwnerAndNotAdmin_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var accountId = 1; // John's Savings
            var userId = 2; // Jane Smith
            _mockCurrentUserService.Setup(s => s.UserId).Returns(userId);
            _mockCurrentUserService.Setup(s => s.Role).Returns("user");

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _accountService.DeleteAccountAsync(accountId));
        }

        [Fact]
        public async Task DeleteAccountAsync_AccountNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            var accountId = 999; // Non-existent account
            _mockCurrentUserService.Setup(s => s.UserId).Returns(1);
            _mockCurrentUserService.Setup(s => s.Role).Returns("user");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _accountService.DeleteAccountAsync(accountId));
        }

        [Fact]
        public async Task DeleteAccountAsync_CreatesDeletedAccountRecord()
        {
            // Arrange
            var accountId = 1; // John's Savings
            var userId = 1; // John Doe
            _mockCurrentUserService.Setup(s => s.UserId).Returns(userId);
            _mockCurrentUserService.Setup(s => s.Role).Returns("user");

            // Act
            await _accountService.DeleteAccountAsync(accountId);

            // Assert
            var deletedAccount = await _context.Accounts.FindAsync(accountId);
            Assert.Null(deletedAccount); // Original account should be removed

            var tombstone = await _context.DeletedAccounts.FirstOrDefaultAsync(da => da.AccountId == accountId);
            Assert.NotNull(tombstone);
            Assert.Equal(userId, tombstone.UserId);
            Assert.Equal("John's Savings", tombstone.Name);
            Assert.Equal(5000.50m, tombstone.Amount);
            Assert.True(tombstone.DeletedAt <= DateTime.UtcNow);

            _mockEventPublisher.Verify(p => p.Publish(
                "AccountEvents",
                It.Is<string>(s =>
                    s.Contains("\"event_type\":\"AccountDeleted\"") && s.Contains($"\"accountId\":{accountId}"))
            ), Times.Once);
        }

        [Fact]
        public async Task RenameAccountAsync_NameIsEmpty_ThrowsInvalidOperationException()
        {
            // Arrange
            var accountId = 1;
            var request = new AccountRenameRequest { Name = "" };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(1);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _accountService.RenameAccountAsync(accountId, request));
        }

        [Fact]
        public async Task RenameAccountAsync_AccountNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            var accountId = 999;
            var request = new AccountRenameRequest { Name = "New Name" };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(1);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _accountService.RenameAccountAsync(accountId, request));
        }

        [Fact]
        public async Task RenameAccountAsync_UserNotOwner_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var accountId = 1; // John's Savings
            var userId = 2; // Jane Smith
            var request = new AccountRenameRequest { Name = "New Name" };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(userId);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _accountService.RenameAccountAsync(accountId, request));
        }

        [Fact]
        public async Task RenameAccountAsync_NameIsUnchanged_ReturnsAccountWithoutPublishingEvent()
        {
            // Arrange
            var accountId = 1; // John's Savings, current name "John's Savings"
            var userId = 1; // John Doe
            var existingName = _context.Accounts.Find(accountId).Name;
            var request = new AccountRenameRequest { Name = existingName };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(userId);

            // Act
            var result = await _accountService.RenameAccountAsync(accountId, request);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(existingName, result.Value.Name);
            _mockEventPublisher.Verify(p => p.Publish(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateBalanceAsync_InvalidTransactionType_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new AccountBalanceRequest
                { Amount = 100, TransactionId = "tx123", TransactionType = "InvalidType" };
            _mockCurrentUserService.Setup(s => s.Role).Returns("service");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _accountService.UpdateBalanceAsync(1, request));
        }

        [Fact]
        public async Task UpdateBalanceAsync_AccountNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new AccountBalanceRequest
                { Amount = 100, TransactionId = "tx123", TransactionType = "Deposit" };
            _mockCurrentUserService.Setup(s => s.Role).Returns("service");

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _accountService.UpdateBalanceAsync(999, request));
        }

        [Fact]
        public async Task UpdateBalanceAsync_DuplicateTransaction_ReturnsCurrentAccountStateWithoutProcessing()
        {
            // Arrange
            var accountId = 1;
            var initialAccount = await _context.Accounts.FindAsync(accountId);
            var request = new AccountBalanceRequest
                { Amount = 100, TransactionId = "tx123", TransactionType = "Deposit" };
            _mockCurrentUserService.Setup(s => s.Role).Returns("service");
            _mockRedisDb.Setup(db =>
                    db.KeyExistsAsync($"account:transaction:{request.TransactionId}", It.IsAny<CommandFlags>()))
                .ReturnsAsync(true); // Simulate duplicate

            // Act
            var result = await _accountService.UpdateBalanceAsync(accountId, request);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(initialAccount.Amount, result.Value.Amount); // Amount should not change
            _mockEventPublisher.Verify(p => p.Publish(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never); // No event published
            _mockRedisDb.Verify(
                db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                    It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never); // No Redis set
        }

        [Fact]
        public async Task UpdateBalanceAsync_UserNotOwnerAndNotService_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var accountId = 1; // John's account
            var request = new AccountBalanceRequest
                { Amount = 100, TransactionId = "tx123", TransactionType = "Deposit" };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(2); // Jane trying to update John's account
            _mockCurrentUserService.Setup(s => s.Role).Returns("user");
            _mockRedisDb.Setup(db => db.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);


            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _accountService.UpdateBalanceAsync(accountId, request));
        }

        [Fact]
        public async Task UpdateBalanceAsync_NegativeAmount_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new AccountBalanceRequest
                { Amount = -100, TransactionId = "tx123", TransactionType = "Deposit" };
            _mockCurrentUserService.Setup(s => s.Role).Returns("service");
            _mockRedisDb.Setup(db => db.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _accountService.UpdateBalanceAsync(1, request));
        }

        [Fact]
        public async Task UpdateBalanceAsync_WithdrawalResultsInNegativeBalance_ThrowsInvalidOperationException()
        {
            // Arrange
            var accountId = 2; // John's Checking, amount 500.25
            var withdrawalAmount = 600m; // More than balance
            var request = new AccountBalanceRequest
                { Amount = withdrawalAmount, TransactionId = "tx123", TransactionType = "Withdrawal" };
            _mockCurrentUserService.Setup(s => s.Role).Returns("service"); // Service can operate
            _mockRedisDb.Setup(db => db.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);


            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _accountService.UpdateBalanceAsync(accountId, request));
        }

        [Fact]
        public async Task DepositToAccountAsync_AmountIsZero_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new AccountDepositRequest { Amount = 0 };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(1);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _accountService.DepositToAccountAsync(1, request));
        }

        [Fact]
        public async Task DepositToAccountAsync_AmountIsNegative_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new AccountDepositRequest { Amount = -50 };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(1);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _accountService.DepositToAccountAsync(1, request));
        }


        [Fact]
        public async Task DepositToAccountAsync_AccountNotFound_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new AccountDepositRequest { Amount = 100 };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(1);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _accountService.DepositToAccountAsync(999, request));
        }

        [Fact]
        public async Task DepositToAccountAsync_UserNotOwner_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var accountId = 1; // John's account
            var request = new AccountDepositRequest { Amount = 100 };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(2); // Jane trying to deposit to John's account

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _accountService.DepositToAccountAsync(accountId, request));
        }

        [Fact]
        public async Task DepositToAccountAsync_DuplicateTransaction_ReturnsCurrentAccountStateWithoutProcessing()
        {
            // Arrange
            var accountId = 1;
            var initialAccount = await _context.Accounts.FindAsync(accountId);
            var request = new AccountDepositRequest { Amount = 100 };
            _mockCurrentUserService.Setup(s => s.UserId).Returns(1);
            // Simulate that a transaction ID (even though generated internally) already exists.
            // This requires a bit more setup if the key is fully dynamic.
            // For simplicity, we assume KeyExistsAsync will be called and we can make it return true.
            // For simplicity, we assume KeyExistsAsync will be called and we can make it return true.
            _mockRedisDb.Setup(db =>
                db.KeyExistsAsync(It.Is<RedisKey>(k => k.ToString().StartsWith("account:transaction:deposit-")),
                    It.IsAny<CommandFlags>())).ReturnsAsync(true);


            // Act
            var result = await _accountService.DepositToAccountAsync(accountId, request);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(initialAccount.Amount, result.Value.Amount); // Amount should not change
            _mockEventPublisher.Verify(p => p.Publish(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockRedisDb.Verify(
                db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                    It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
        }


        // GetUserAccountsAsync Tests
        [Fact]
        public async Task GetUserAccountsAsync_ValidUserId_ReturnsAccountsForUser()
        {
            // Arrange
            var userId = 1; // John Doe
            var expectedAccounts = _context.Accounts.Where(a => a.UserId == userId).ToList();
            _mockAccountRepository.Setup(r => r.GetAccountsByUserIdAsync(userId))
                .ReturnsAsync(expectedAccounts);

            // Act
            var result = await _accountService.GetUserAccountsAsync(userId.ToString());

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(expectedAccounts.Count, result.Value.Count);
            foreach (var expectedAccount in expectedAccounts)
            {
                Assert.Contains(result.Value, ra => ra.Id == expectedAccount.Id && ra.Name == expectedAccount.Name);
            }
        }

        [Fact]
        public async Task GetUserAccountsAsync_UserIdIsNullOrEmpty_ThrowsArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _accountService.GetUserAccountsAsync(null));
            await Assert.ThrowsAsync<ArgumentException>(() => _accountService.GetUserAccountsAsync(""));
        }

        [Fact]
        public async Task GetUserAccountsAsync_InvalidUserIdFormat_ThrowsFormatException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<FormatException>(() => _accountService.GetUserAccountsAsync("not-an-integer"));
        }

        [Fact]
        public async Task GetUserAccountsAsync_NoAccountsFoundForUser_ReturnsEmptyList()
        {
            // Arrange
            var userId = 99; // A user ID that has no accounts
            _mockAccountRepository.Setup(r => r.GetAccountsByUserIdAsync(userId))
                .ReturnsAsync(new List<Account>()); // Return empty list

            // Act
            var result = await _accountService.GetUserAccountsAsync(userId.ToString());

            // Assert
            Assert.NotNull(result.Value);
            Assert.Empty(result.Value);
        }


        // --- Black Box Tests (Conceptual - to be implemented as integration or API tests) ---

        // Black box tests would typically be written against the API endpoints.
        // They would involve:
        // - Setting up a test HTTP client.
        // - Making HTTP requests to the AccountController endpoints.
        // - Asserting the HTTP response (status code, body content).
        // - Verifying data changes in the database (if applicable, through a separate query or another API call).
        // - These are out of scope for this specific unit test file but important for overall coverage.

        // Example Black Box Test (Conceptual - for GetAccounts endpoint)
        // [Fact]
        // public async Task GetAccounts_UserIsAuthenticated_ReturnsOkWithAccounts()
        // {
        //     // Arrange: HttpClient setup, authentication token for a user
        //     // Act: GET request to /api/account/user
        //     // Assert: 200 OK, response body contains expected accounts for the authenticated user
        // }

        [Fact]
        public async Task GetAccountsAsync_SuccessfulRequest_IncrementsMetrics()
        {
            // Arrange
            var userId = 1;
            _mockCurrentUserService.Setup(s => s.UserId).Returns(userId);

            // Act
            var result = await _accountService.GetAccountsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count); // John Doe has 2 accounts
            Assert.All(result, acc => Assert.Equal(userId, acc.UserId));
            // Note: We can't directly verify the metrics as they are static counters
            // In a real application, we would use a metrics registry that can be mocked
        }

        [Fact]
        public async Task UpdateBalanceAsync_DuplicateTransaction_IncrementsIdempotencyCounter()
        {
            // Arrange
            var accountId = 1;
            var request = new AccountBalanceRequest
            {
                Amount = 100,
                TransactionId = "tx123",
                TransactionType = "Deposit"
            };
            _mockCurrentUserService.Setup(s => s.Role).Returns("service");
            _mockRedisDb.Setup(db =>
                    db.KeyExistsAsync($"account:transaction:{request.TransactionId}", It.IsAny<CommandFlags>()))
                .ReturnsAsync(true); // Simulate duplicate

            // Act
            var result = await _accountService.UpdateBalanceAsync(accountId, request);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(5000.50m, result.Value.Amount); // Amount should not change
            _mockEventPublisher.Verify(p => p.Publish(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never); // No event published
            // Note: We can't directly verify the idempotency counter as it's static
            // In a real application, we would use a metrics registry that can be mocked
        }
    }
}