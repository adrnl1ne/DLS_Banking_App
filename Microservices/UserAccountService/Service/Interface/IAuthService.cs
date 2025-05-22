using Microsoft.AspNetCore.Mvc;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service;

public interface IAuthService
{
    Task<ActionResult> LoginAsync(string usernameOrEmail, string password);
    Task<ActionResult> GenerateServiceTokenAsync(string serviceName);
    Task<ActionResult> GetUsersAsync();
    Task<ActionResult> CreateUserAsync(UserRequest userRequest);
}
