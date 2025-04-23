using AccountService.Database.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using UserAccountService.Shared.DTO;

namespace UserAccountService.Service;

public class AuthService(UserAccountDbContext context, IConfiguration configuration) : IAuthService
{
    public async Task<ActionResult> LoginAsync(string usernameOrEmail, string password)
    {
        // Find user by email or username
        var user = await context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == usernameOrEmail || u.Username == usernameOrEmail);

        if (user == null)
        {
            return new UnauthorizedObjectResult("Invalid username/email or password.");
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
        {
            return new UnauthorizedObjectResult("Invalid username/email or password.");
        }

        // Generate JWT token
        var jwtKey = configuration.GetValue<string>("JWT_KEY") ??
                     throw new InvalidOperationException("JWT_KEY not configured");
        var jwtIssuer = configuration.GetValue<string>("JWT_ISSUER") ??
                        throw new InvalidOperationException("JWT_ISSUER not configured");
        var jwtAudience = configuration.GetValue<string>("JWT_AUDIENCE") ??
                          throw new InvalidOperationException("JWT_AUDIENCE not configured");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.NameId, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("role", user.Role?.Name.ToLower() ?? "user"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.Now.AddMinutes(30),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new OkObjectResult(new { Token = tokenString });
    }
}
