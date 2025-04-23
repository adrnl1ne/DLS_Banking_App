using Microsoft.AspNetCore.Mvc;

namespace UserAccountService.Service;

public interface IAuthService
{
    Task<ActionResult> LoginAsync(string usernameOrEmail, string password);
}
