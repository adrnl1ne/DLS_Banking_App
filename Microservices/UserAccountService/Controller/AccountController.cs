using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using UserAccountService.Service;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Controller;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AccountController(IAccountService accountService) : ControllerBase
{
    [HttpGet("test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult TestEndpoint()
    {
        return Ok(new { Message = "Test endpoint reached successfully!" });
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<AccountResponse>>> GetAccounts()
    {
        return await accountService.GetAccountsAsync();
    }

    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> GetAccount(int id)
    {
        return await accountService.GetAccountAsync(id);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountResponse>> CreateAccount([FromBody] AccountCreationRequest request)
    {
        return await accountService.CreateAccountAsync(request);
    }
}
