using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Prometheus;
using TransactionService.Infrastructure.Data;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Infrastructure.Logging;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Infrastructure.Security;
using TransactionService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "Transaction API", 
        Version = "v1",
        Description = "API for managing banking transactions",
        Contact = new OpenApiContact
        {
            Name = "Banking App Team",
            Email = "support@bankingapp.com"
        }
    });
    
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
            Array.Empty<string>()
        }
    });
});

var connectionString = string.Format("server={0};port={1};database={2};user={3};password={4};SslMode=Required",
    builder.Configuration.GetValue<string>("MYSQL_HOST"),
    builder.Configuration.GetValue<string>("MYSQL_PORT"),
    builder.Configuration.GetValue<string>("MYSQL_DATABASE"),
    builder.Configuration.GetValue<string>("MYSQL_USER"),
    builder.Configuration.GetValue<string>("MYSQL_PASSWORD"));

// Register the DbContext with MySQL configuration
builder.Services.AddDbContext<TransactionDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
        mySqlOptions => mySqlOptions.EnableStringComparisonTranslations())
);

// Configure Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Get configuration values with defaults
        var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? 
                        builder.Configuration["JWT:Issuer"] ?? 
                        "BankingApp";
        
        var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? 
                          builder.Configuration["JWT:Audience"] ?? 
                          "TransactionAPI";
        
        var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? 
                     builder.Configuration["JWT:Key"] ?? 
                     "default-development-signing-key-min-16-chars";

        Console.WriteLine($"Configuring JWT authentication with Issuer: {jwtIssuer}, Audience: {jwtAudience}");
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Configure HttpClient for User Account Service

// Setup Environment Variables support
builder.Configuration.AddEnvironmentVariables();

// Add logging filter
builder.Logging.AddSensitiveDataFilter();

// Add secure transaction logger
builder.Services.AddScoped<TransactionService.Infrastructure.Logging.ISecureTransactionLogger, 
                          TransactionService.Infrastructure.Logging.SecureTransactionLogger>();

// Replace the token configuration (which is currently exposing the token in the logs)
builder.Services.AddHttpClient<UserAccountClientService>((services, client) => {
    var configuration = services.GetRequiredService<IConfiguration>();
    var logger = services.GetRequiredService<ILogger<UserAccountClientService>>();
    
    // Get token from configuration
    var serviceToken = Environment.GetEnvironmentVariable("TRANSACTION_SERVICE_TOKEN") ?? 
                  configuration["ServiceAuthentication:Token"] ?? 
                  throw new InvalidOperationException("TransactionService token not configured");
        
    client.BaseAddress = new Uri(configuration["Services:UserAccountService"] ?? "http://user-account-service:80");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
    
    // Don't log the token
    logger.LogInformation("UserAccountClientService configured with BaseAddress: {BaseAddress}", client.BaseAddress);
});

// Production logging configuration
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
    builder.Logging.AddFilter("System", LogLevel.Warning);
}

var app = builder.Build();

app.Run();
