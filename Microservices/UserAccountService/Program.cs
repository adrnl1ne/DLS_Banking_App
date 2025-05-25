using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AccountService.Database.Data;
using AccountService.Repository;
using UserAccountService.Repository;
using UserAccountService.Service; // Changed from UserAccountService.Services
using Microsoft.OpenApi.Models;
using Prometheus;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using UserAccountService.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks(); // Added health checks registration
// Add logging services
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
});

// Add services to the container.
builder.Services.AddControllers();

// Add CORS configuration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",
            "http://localhost:3001", 
            "http://localhost:5173", // Vite dev server
            "http://localhost:4173"  // Vite preview
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Register repositories and services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, UserAccountService.Service.AccountService>();

// Register Redis IConnectionMultiplexer
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
    if (string.IsNullOrEmpty(redisConnectionString))
    {
        var host = builder.Configuration.GetValue<string>("REDIS_HOST") ?? "redis";
        var port = builder.Configuration.GetValue<string>("REDIS_PORT") ?? "6379";
        redisConnectionString = $"{host}:{port}";
    }

    return ConnectionMultiplexer.Connect(redisConnectionString);
});

// Configure Redis cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ??
                            $"{builder.Configuration.GetValue<string>("REDIS_HOST", "redis")}:{builder.Configuration.GetValue<string>("REDIS_PORT", "6379")}";
    options.InstanceName = "UserAccountService_";
});

// Configure MySQL
var connectionString = string.Format(
    "server={0};port={1};database={2};user={3};password={4};SslMode=Required",
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
builder.Services.AddAuthorizationBuilder()
    // Add authorization policies
    .AddPolicy("ReadAccounts", policy =>
        policy.RequireRole("service")
            .RequireClaim("scopes", "read:accounts"))
    // Add authorization policies
    .AddPolicy("ServiceOnly", policy =>
        policy.RequireRole("service"));

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "UserAccountService API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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

// Register RabbitMQ client with correct connection parameters
builder.Services.AddSingleton<IRabbitMqClient>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<RabbitMqClient>>();
    
    // Get RabbitMQ connection details from configuration or environment variables
    var host = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? 
                Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? 
                "rabbitmq";
                
    var portStr = builder.Configuration.GetValue<string>("RabbitMQ:Port") ?? 
                  Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? 
                  "5672";
    var port = int.TryParse(portStr, out var p) ? p : 5672;
    
    var username = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? 
                   Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? 
                   "guest";
                   
    var password = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? 
                   Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? 
                   "guest";
    
    return new RabbitMqClient(logger, host, port, username, password);
});

// Register message processing services
builder.Services.AddScoped<AccountBalanceProcessingService>();

// Register the consumer background service with appropriate lifetime
builder.Services.AddHostedService<AccountBalanceConsumerService>();

// Register HTTP client factory
builder.Services.AddHttpClient("InternalApi", client => {
    client.BaseAddress = new Uri("http://localhost:80"); // Points to the service itself
    client.DefaultRequestHeaders.Add("X-Internal-Request", "true");
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add CORS middleware BEFORE authentication and authorization
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();