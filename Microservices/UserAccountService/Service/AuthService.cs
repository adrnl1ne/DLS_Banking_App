using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using BCrypt.Net;
using UserAccountService.Shared.DTO;
using AccountService.Database.Data;
using AccountService.Models;

namespace UserAccountService.Service;

public class AuthService : IAuthService
{
    private readonly UserAccountDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(UserAccountDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;

        // Log configuration values for debugging
        _logger.LogInformation("AuthService initialized with JWT_ISSUER: {Issuer}, JWT_AUDIENCE: {Audience}", 
            configuration["JWT_ISSUER"], configuration["JWT_AUDIENCE"]);
    }

    public async Task<ActionResult> LoginAsync(string usernameOrEmail, string password)
    {
        // Find user by email or username
        var user = await _context.Users
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
        var tokenString = GenerateUserToken(user);
        return new OkObjectResult(new { Token = tokenString });
    }

    public Task<ActionResult> GenerateServiceTokenAsync(string serviceName)
    {
        if (string.IsNullOrEmpty(serviceName))
        {
            return Task.FromResult<ActionResult>(new BadRequestObjectResult("Service name must be provided."));
        }

        var tokenString = GenerateServiceToken(serviceName);
        return Task.FromResult<ActionResult>(new OkObjectResult(new { Token = tokenString }));
    }

    private string GenerateUserToken(User user)
    {
        var jwtKey = _configuration.GetValue<string>("JWT_KEY") ??
                     throw new InvalidOperationException("JWT_KEY not configured");
        var jwtIssuer = _configuration.GetValue<string>("JWT_ISSUER") ??
                        throw new InvalidOperationException("JWT_ISSUER not configured");
        var jwtAudience = _configuration.GetValue<string>("JWT_AUDIENCE") ??
                          throw new InvalidOperationException("JWT_AUDIENCE not configured");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
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
        _logger.LogInformation("Generated user JWT token: {Token}", tokenString);
        return tokenString;
    }

    private string GenerateServiceToken(string serviceName)
    {
        var jwtKey = _configuration.GetValue<string>("JWT_KEY") ??
                     throw new InvalidOperationException("JWT_KEY not configured");
        var jwtIssuer = _configuration.GetValue<string>("JWT_ISSUER") ??
                        throw new InvalidOperationException("JWT_ISSUER not configured");
        var jwtAudience = _configuration.GetValue<string>("JWT_AUDIENCE") ??
                          throw new InvalidOperationException("JWT_AUDIENCE not configured");

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, serviceName),
            new Claim("role", "service"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iss, jwtIssuer)
        };

        // Add scopes as separate claims
        claims.Add(new Claim("scopes", "read:accounts"));
        claims.Add(new Claim("scopes", "update:account-balance"));

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.Now.AddMonths(6),
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        _logger.LogInformation("Generated service JWT token for {ServiceName}: {Token}", serviceName, tokenString);
        return tokenString;
    }
}
