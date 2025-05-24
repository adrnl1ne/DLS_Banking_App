using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using UserAccountService.Service;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Controller;

/// <summary>
///  This controller handles account-related operations.
/// </summary>
/// <param name="accountService"></param>
/// <param name="logger"></param>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AccountController(IAccountService accountService, ILogger<AccountController> logger)
    : ControllerBase
{
    /// <summary>
    /// Retrieves a list of all accounts.
    /// </summary>
    /// <returns>
    /// Returns a list of all accounts.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<AccountResponse>>> GetAccounts()
    {
        var result = await accountService.GetAccountsAsync();
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a specific account by its ID.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>
    /// Returns the account with the specified ID.
    /// </returns>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> GetAccount(int id)
    {
        return await accountService.GetAccountAsync(id);
    }

    /// <summary>
    /// Retrieves a list of accounts for the authenticated user.
    /// </summary>
    /// <returns>
    /// Returns a list of accounts associated with the authenticated user.
    /// </returns>
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

    /// <summary>
    /// Creates a new account.
    /// </summary>
    /// <param name="request"></param>
    /// <returns>
    /// Returns the created account.
    /// </returns>
    [HttpPost]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AccountResponse>> CreateAccount([FromBody] AccountCreationRequest request)
    {
        return await accountService.CreateAccountAsync(request);
    }

    /// <summary>
    /// Update the balance of an account.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="request"></param>
    /// <returns>
    /// Returns AccountResponse with updated balance.
    /// </returns>
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

    /// <summary>
    /// Used to deposit money into an account.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("{id}/deposit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> Deposit(int id, [FromBody] AccountDepositRequest request)
    {
        logger.LogInformation("Received deposit request for account {AccountId}", id);
        return await accountService.DepositToAccountAsync(id, request);
    }

    /// <summary>
    /// Used to delete an account.
    /// </summary>
    /// <param name="id"></param>
    /// <returns>
    /// Return 204 No Content if the account is deleted successfully.
    /// </returns>
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

    /// <summary>
    /// Retrieves all accounts in the system.
    /// This endpoint is restricted to service callers only.
    /// </summary>
    /// <returns>
    /// Returns a list of all accounts in the system.
    /// </returns>
    [HttpGet("all")]
    [Authorize(Policy = "ServiceOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<AccountResponse>>> GetAllAccounts()
    {
        logger.LogInformation("Service requesting all accounts");
        var accounts = await accountService.GetAllAccountsAsServiceAsync();
        return Ok(accounts);
    }
}