using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AccountService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AccountService.Controller;

public class TokenController(IConfiguration configuration) : ControllerBase
{
    [HttpGet("generate")]
    public IActionResult GenerateToken(int id, string email, string role)
    {
        
        var jwtKey = configuration.GetValue<string>("JWT_KEY") ?? throw new InvalidOperationException("JWT_KEY not configured");
        var jwtIssuer = configuration.GetValue<string>("JWT_ISSUER") ?? throw new InvalidOperationException("JWT_ISSUER not configured");
        var jwtAudience = configuration.GetValue<string>("JWT_AUDIENCE") ?? throw new InvalidOperationException("JWT_AUDIENCE not configured");

        Console.WriteLine($"Token Generation - Issuer: {jwtIssuer}, Audience: {jwtAudience}, Key: {jwtKey.Substring(0, 10)}...");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.NameId, id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("role", role.ToLower()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new { Token = tokenString });
    }
}
