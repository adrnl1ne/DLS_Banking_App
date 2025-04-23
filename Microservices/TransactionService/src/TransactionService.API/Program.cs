using System;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TransactionService.API.Infrastructure.Data;
using TransactionService.API.Infrastructure.Data.Repositories;
using TransactionService.API.Infrastructure.Messaging.RabbitMQ;
using TransactionService.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Add this line near the top to force Development environment
builder.Environment.EnvironmentName = "Development";

// Add services to the container
builder.Services.AddControllers();

// Configure DbContext
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

// Configure RabbitMQ
var rabbitMQConfig = new RabbitMQConfiguration
{
    HostName = builder.Configuration["RABBITMQ_HOST"] ?? "rabbitmq",
    Port = int.Parse(builder.Configuration["RABBITMQ_PORT"] ?? "5672"),
    UserName = builder.Configuration["RABBITMQ_USERNAME"] ?? "guest",
    Password = builder.Configuration["RABBITMQ_PASSWORD"] ?? "guest",
    VirtualHost = builder.Configuration["RABBITMQ_VHOST"] ?? "/",
};
builder.Services.AddSingleton(rabbitMQConfig);
builder.Services.AddSingleton<IRabbitMQClient, RabbitMQClient>();

// Register services and repositories
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ITransactionService, TransactionService.API.Services.TransactionService>();

// Configure JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ThisIsMySecureKeyWithAtLeast32Characters";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "UserAccountService";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BankingApp";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "Transaction Service API", 
        Version = "v1",
        Description = "Transaction Service API for the DLS Banking App<br/><br/>" +
                    "**Development Testing:** Generate a test token using the `/api/Dev/token` endpoint first, " +
                    "then click the 'Authorize' button and enter: `Bearer your-token-here`"
    });
    
    // Add JWT Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.<br/>" +
                    "Enter 'Bearer' [space] and then your token in the text input below.<br/>" +
                    "Example: 'Bearer 12345abcdef'",
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
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || true) // Force Swagger UI to be available in all environments for testing
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction Service API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Setup RabbitMQ consumer for fraud detection results
var scope = app.Services.CreateScope();
var transactionService = scope.ServiceProvider.GetRequiredService<ITransactionService>();
var rabbitMQClient = scope.ServiceProvider.GetRequiredService<IRabbitMQClient>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

try
{
    rabbitMQClient.SubscribeToQueue<object>("TransactionServiceQueue", async message =>
    {
        var jsonMessage = JsonSerializer.Serialize(message);
        logger.LogInformation("Received fraud detection result: {Message}", jsonMessage);
        
        if (message != null)
        {
            var json = JsonSerializer.Serialize(message);
            var doc = JsonDocument.Parse(json);
            
            string? transferId = null;
            bool isFraud = false;
            string status = "approved";
            
            if (doc.RootElement.TryGetProperty("transferId", out var transferIdElement))
            {
                transferId = transferIdElement.GetString();
            }
            
            if (doc.RootElement.TryGetProperty("isFraud", out var isFraudElement) &&
                isFraudElement.ValueKind == JsonValueKind.True)
            {
                isFraud = true;
                status = "declined";
            }
            
            if (doc.RootElement.TryGetProperty("status", out var statusElement) &&
                statusElement.ValueKind == JsonValueKind.String)
            {
                status = statusElement.GetString() ?? status;
            }
            
            if (!string.IsNullOrEmpty(transferId))
            {
                await transactionService.HandleFraudDetectionResultAsync(transferId, isFraud, status);
            }
        }
    });
}
catch (Exception ex)
{
    logger.LogError(ex, "Error setting up RabbitMQ subscription");
}

// Ensure database is created
using (var serviceScope = app.Services.CreateScope())
{
    var dbContext = serviceScope.ServiceProvider.GetRequiredService<TransactionDbContext>();
    try
    {
        // Try to connect to the database and execute a simple command
        // This will verify connection without using EnsureCreated()
        dbContext.Database.OpenConnection();
        dbContext.Database.CloseConnection();
        
        logger.LogInformation("Database connection verified successfully");
        
        // Create tables using SQL directly if needed
        var connection = dbContext.Database.GetDbConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        
        // Check if Transactions table exists
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS `Transactions` (
                `Id` CHAR(36) NOT NULL,
                `TransferId` VARCHAR(255) NOT NULL,
                `FromAccount` VARCHAR(255) NOT NULL,
                `ToAccount` VARCHAR(255) NOT NULL,
                `Amount` DECIMAL(18, 2) NOT NULL,
                `Status` VARCHAR(50) NOT NULL,
                `CreatedAt` DATETIME NOT NULL,
                `UpdatedAt` DATETIME NULL,
                PRIMARY KEY (`Id`),
                UNIQUE INDEX `IX_Transactions_TransferId` (`TransferId`)
            );";
        
        command.ExecuteNonQuery();
        connection.Close();
        
        logger.LogInformation("Database schema setup completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while setting up the database");
    }
}

app.Run();
