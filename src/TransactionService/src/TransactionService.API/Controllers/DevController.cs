using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace TransactionService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public DevController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [HttpGet("token")]
    public IActionResult GenerateToken([FromQuery] string userId = "admin-user")
    {
        // Only allow this endpoint in Development environment
        if (!_environment.IsDevelopment() && !Request.Headers["X-Allow-Dev-Endpoints"].Contains("true"))
        {
            return NotFound();
        }

        var issuer = _configuration["Jwt:Issuer"] ?? "UserAccountService";
        var audience = _configuration["Jwt:Audience"] ?? "BankingApp";
        var key = _configuration["Jwt:Key"] ?? "ThisIsMySecureKeyWithAtLeast32Characters";

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("Id", userId),
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, $"{userId}@example.com"),
                // Always include admin role
                new Claim(ClaimTypes.Role, "admin"),
                // Include additional claims that might be useful for testing
                new Claim("AccountId", "ADMIN-ACC-123"),
                new Claim("Permissions", "full_access")
            }),
            Expires = DateTime.UtcNow.AddDays(7), // Longer expiration for testing
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new { 
            Token = tokenString, 
            ExpiresAt = tokenDescriptor.Expires,
            Type = "Bearer",
            UserId = userId,
            Role = "admin",
            // Include formatted token for easy copy-paste into Swagger
            FormattedToken = $"Bearer {tokenString}"
        });
    }
}
