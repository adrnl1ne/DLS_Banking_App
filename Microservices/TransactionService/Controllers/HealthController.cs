using Microsoft.AspNetCore.Mvc;


namespace TransactionService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        logger.LogInformation("Health check endpoint called");
        return Ok(new { status = "healthy", service = "Transaction Service", timestamp = DateTime.UtcNow });
    }
}
