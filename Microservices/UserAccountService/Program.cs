using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using System.Text;
using AccountService.Database.Data;
using AccountService.Repository;
using AccountService.Services;
using UserAccountService.Repository;
using UserAccountService.Service;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Register repositories and services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, UserAccountService.Service.AccountService>();

var connectionString = string.Format("server={0};port={1};database={2};user={3};password={4};SslMode=Required",
    builder.Configuration.GetValue<string>("MYSQL_HOST"),
    builder.Configuration.GetValue<string>("MYSQL_PORT"),
    builder.Configuration.GetValue<string>("MYSQL_DATABASE"),
    builder.Configuration.GetValue<string>("MYSQL_USER"),
    builder.Configuration.GetValue<string>("MYSQL_PASSWORD"));

builder.Services.AddDbContext<UserAccountDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
        mySqlOptions => mySqlOptions.EnableStringComparisonTranslations()));

// Configure JWT Authentication
var jwtIssuer = builder.Configuration.GetValue<string>("JWT_ISSUER");
var jwtAudience = builder.Configuration.GetValue<string>("JWT_AUDIENCE");
var jwtKey = builder.Configuration.GetValue<string>("JWT_KEY") ??
             throw new InvalidOperationException("JWT Key must be configured");

Console.WriteLine($"JWT Configuration: Issuer={jwtIssuer}, Audience={jwtAudience}, Key={jwtKey.Substring(0, 10)}...");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        NameClaimType = ClaimTypes.NameIdentifier
    };
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Token validation failed: {ExceptionMessage}", context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ReadAccounts", policy =>
        policy.RequireRole("service")
              .RequireClaim("scopes", "read:accounts"));
});

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UserAccountService API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "UserAccountService API v1");
    c.RoutePrefix = string.Empty;
});

app.Run();
