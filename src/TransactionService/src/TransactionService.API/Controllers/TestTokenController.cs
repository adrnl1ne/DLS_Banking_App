using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace TransactionService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestTokenController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public TestTokenController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult GenerateToken()
    {
        var issuer = _configuration["Jwt:Issuer"] ?? "UserAccountService";
        var audience = _configuration["Jwt:Audience"] ?? "BankingApp";
        var key = _configuration["Jwt:Key"] ?? "ThisIsMySecureKeyWithAtLeast32Characters";

        var claims = new[]
        {
            new Claim("Id", "admin-user"),
            new Claim(JwtRegisteredClaimNames.Sub, "admin-user"),
            new Claim(JwtRegisteredClaimNames.Email, "admin@example.com"),
            new Claim(ClaimTypes.Role, "admin")
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256)
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new { Token = tokenString, FormattedToken = $"Bearer {tokenString}" });
    }
}