using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TransactionService.Controllers;
using TransactionService.Models;
using TransactionService.Services;
using Xunit;

namespace TransactionService.Tests.Controllers
{
    public class TransactionControllerTests
    {
        private readonly Mock<ITransactionService> _mockTransactionService;
        private readonly Mock<ILogger<TransactionController>> _mockLogger;
        private readonly TransactionController _controller;

        public TransactionControllerTests()
        {
            _mockTransactionService = new Mock<ITransactionService>();
            _mockLogger = new Mock<ILogger<TransactionController>>();
            _controller = new TransactionController(_mockTransactionService.Object, _mockLogger.Object);
            
            // Setup the controller with a ClaimsPrincipal
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "123"),
                new Claim(ClaimTypes.Name, "testuser")
            }, "mock"));
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public async Task CreateTransfer_WithValidRequest_ReturnsCreatedAtAction()
        {
            // Arrange
            var request = new TransactionRequest
            {
                FromAccount = "123456",
                ToAccount = "654321",
                Amount = 100
            };
            
            var response = new TransactionResponse
            {
                TransferId = "TRX-123",
                FromAccount = "123456",
                ToAccount = "654321",
                Amount = 100,
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };
            
            _mockTransactionService.Setup(s => s.CreateTransferAsync(It.IsAny<TransactionRequest>()))
                .ReturnsAsync(response);
            
            // Act
            var result = await _controller.CreateTransfer(request);
            
            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<TransactionResponse>(createdAtActionResult.Value);
            Assert.Equal(response.TransferId, returnValue.TransferId);
            Assert.Equal(123, request.UserId); // Check that UserId was set from claims
        }

        [Fact]
        public async Task GetTransaction_WithValidId_ReturnsOk()
        {
            // Arrange
            var transferId = "TRX-123";
            var response = new TransactionResponse
            {
                TransferId = transferId,
                FromAccount = "123456",
                ToAccount = "654321",
                Amount = 100,
                Status = "completed",
                CreatedAt = DateTime.UtcNow
            };
            
            _mockTransactionService.Setup(s => s.GetTransactionByTransferIdAsync(transferId))
                .ReturnsAsync(response);
            
            // Act
            var result = await _controller.GetTransaction(transferId);
            
            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<TransactionResponse>(okResult.Value);
            Assert.Equal(transferId, returnValue.TransferId);
        }

        [Fact]
        public async Task GetTransactionsByAccount_WithValidId_ReturnsOk()
        {
            // Arrange
            var accountId = "123456";
            var responses = new List<TransactionResponse>
            {
                new TransactionResponse
                {
                    TransferId = "TRX-123",
                    FromAccount = accountId,
                    ToAccount = "654321",
                    Amount = 100,
                    Status = "completed",
                    CreatedAt = DateTime.UtcNow
                },
                new TransactionResponse
                {
                    TransferId = "TRX-456",
                    FromAccount = "654321",
                    ToAccount = accountId,
                    Amount = 200,
                    Status = "completed",
                    CreatedAt = DateTime.UtcNow
                }
            };
            
            _mockTransactionService.Setup(s => s.GetTransactionsByAccountAsync(accountId))
                .ReturnsAsync(responses);
            
            // Act
            var result = await _controller.GetTransactionsByAccount(accountId);
            
            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<TransactionResponse>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
        }
    }
}