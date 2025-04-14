using Microsoft.AspNetCore.Mvc;
using UserAccountService.Service;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Controller;

[Route("api/[controller]")]
[ApiController]
public class TokenController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
    {
        return await authService.LoginAsync(loginModel.UsernameOrEmail, loginModel.Password);
    }
}
