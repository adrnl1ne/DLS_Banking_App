// Minimum required imports
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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

// Configure Swagger with authentication support
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
    
    // Add JWT authentication to Swagger UI
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
builder.Services.AddAuthentication(options => {
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Get configuration values with defaults
    var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? 
                    builder.Configuration["JWT:Issuer"] ?? 
                    "BankingApp";
    
    var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? 
                      builder.Configuration["JWT:Audience"] ?? 
                      "UserAccountAPI";
    
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

builder.Services.AddAuthorization();

// Setup Environment Variables support
builder.Configuration.AddEnvironmentVariables();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add secure transaction logger
builder.Services.AddScoped<ISecureTransactionLogger, SecureTransactionLogger>();

// Configure UserAccountClientService with proper token handling
builder.Services.AddHttpClient<UserAccountClientService>((services, client) => {
    var configuration = services.GetRequiredService<IConfiguration>();
    var logger = services.GetRequiredService<ILogger<UserAccountClientService>>();
    
    var serviceToken = Environment.GetEnvironmentVariable("TRANSACTION_SERVICE_TOKEN") ?? 
                  configuration["ServiceAuthentication:Token"];
    
    var baseAddress = configuration["Services:UserAccountService"] ?? "http://user-account-service";
    client.BaseAddress = new Uri(baseAddress);
    
    if (!string.IsNullOrEmpty(serviceToken))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
    }
    
    logger.LogInformation("UserAccountClientService initialized with BaseAddress: {BaseAddress}", client.BaseAddress);
});

// Register other required services
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddSingleton<IRabbitMqClient, RabbitMqClient>();
builder.Services.AddSingleton<Counter>(Metrics.CreateCounter("transactions_total", "The total number of transactions"));
builder.Services.AddSingleton<Histogram>(Metrics.CreateHistogram("transaction_amount", "Transaction amounts"));
builder.Services.AddScoped<ITransactionService, TransactionService.Services.TransactionService>();
builder.Services.AddSingleton<FallbackFraudService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseDeveloperExceptionPage();
}

// CRITICAL: Set up Swagger UI before authentication middleware
app.UseSwagger();
app.UseSwaggerUI(c => 
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction API v1");
    c.RoutePrefix = string.Empty; // Serve at root path
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None); // Don't expand by default
});

// The middleware ordering is critical for proper functioning
app.UseRouting();                // First set up routing
app.UseCors("AllowAll");         // Then CORS
app.UseMetricServer();           // Then metrics before auth
app.UseAuthentication();         // Then authentication
app.UseAuthorization();          // Then authorization
app.MapControllers();            // Finally map controllers

app.Run();
