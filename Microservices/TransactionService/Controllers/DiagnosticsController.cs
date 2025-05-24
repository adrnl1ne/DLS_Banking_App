using Microsoft.AspNetCore.Mvc;
using TransactionService.Infrastructure.Messaging.RabbitMQ;

namespace TransactionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController : ControllerBase
{
    private readonly IRabbitMQClient _rabbitMqClient;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(IRabbitMQClient rabbitMqClient, ILogger<DiagnosticsController> logger)
    {
        _rabbitMqClient = rabbitMqClient;
        _logger = logger;
    }

    [HttpGet("rabbitmq")]
    public IActionResult CheckRabbitMQ()
    {
        try
        {
            // Try declaring a test queue to check if RabbitMQ is available
            _rabbitMqClient.Publish("TestQueue", "Connection test");
            return Ok(new { status = "connected", message = "RabbitMQ connection is working properly" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ connection check failed");
            return StatusCode(500, new { status = "disconnected", message = ex.Message });
        }
    }
}