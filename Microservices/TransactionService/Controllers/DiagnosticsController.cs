using Microsoft.AspNetCore.Mvc;
using TransactionService.Infrastructure.Messaging.RabbitMQ;

namespace TransactionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiagnosticsController(IRabbitMqClient rabbitMqClient, ILogger<DiagnosticsController> logger)
    : ControllerBase
{
    [HttpGet("rabbitmq")]
    public IActionResult CheckRabbitMq()
    {
        try
        {
            // Try declaring a test queue to check if RabbitMQ is available
            rabbitMqClient.Publish("TestQueue", "Connection test");
            return Ok(new { status = "connected", message = "RabbitMQ connection is working properly" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RabbitMQ connection check failed");
            return StatusCode(500, new { status = "disconnected", message = ex.Message });
        }
    }
}