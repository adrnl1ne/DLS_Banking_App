using System.Security.Claims;

namespace UserAccountService.Service;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public int? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null)
            {
                return null;
            }

            // Check if the token is a service token
            var roleClaim = user.FindFirst("role")?.Value ?? user.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim == "service")
            {
                return null; // Service tokens don't have a user ID
            }

            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new InvalidOperationException("User ID claim is missing or invalid.");
            }
            return userId;
        }
    }

    public string Role
    {
        get
        {
            // Check if we're in an HTTP context
            if (_httpContextAccessor.HttpContext == null)
            {
                // Return a system role for background processes
                return "system";
            }

            // Normal HTTP context code
            if (_httpContextAccessor.HttpContext.User == null)
            {
                return "anonymous";
            }

            var role = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            return role ?? "user";
        }
    }
}
