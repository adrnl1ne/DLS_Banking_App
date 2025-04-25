using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<AccountResponse>>> GetAccounts()
    {
       var result = await accountService.GetAccountsAsync();
       return Ok(result);

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
    
    [HttpPut("{id}/balance")]
    [Authorize(Policy = "ServiceOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> UpdateBalance(int id, [FromBody] AccountBalanceRequest request)
    {
        logger.LogInformation("Received balance update request for account {AccountId}", id);
        return await accountService.UpdateBalanceAsync(id, request);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        await accountService.DeleteAccountAsync(id);
        return NoContent();
    }

}