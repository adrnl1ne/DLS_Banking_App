using Microsoft.AspNetCore.Mvc;

namespace UserAccountService.Service;

public interface IAuthService
{
    Task<ActionResult> LoginAsync(string usernameOrEmail, string password);
    Task<ActionResult> GenerateServiceTokenAsync(string serviceName);
}
