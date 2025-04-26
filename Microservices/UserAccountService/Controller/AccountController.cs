using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using UserAccountService.Service;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Controller;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AccountController(IAccountService accountService, ILogger<AccountController> logger)
    : ControllerBase
{
    [HttpGet("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult TestEndpoint()
    {
        logger.LogInformation("Test endpoint called");
        return Ok(new { Message = "Test endpoint reached successfully!" });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> GetAccount(int id)
    {
        var claims = User.Claims.Select(c => $"{c.Type}: {c.Value}");
        logger.LogInformation("GetAccount called with claims: {Claims}", string.Join(", ", claims));
        return await accountService.GetAccountAsync(id);
    }

    [HttpGet("user")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<AccountResponse>>> GetUserAccounts()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        return await accountService.GetUserAccountsAsync(userId);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountResponse>> CreateAccount([FromBody] AccountCreationRequest request)
    {
        logger.LogInformation("CreateAccount called");
        return await accountService.CreateAccountAsync(request);
    }
}
