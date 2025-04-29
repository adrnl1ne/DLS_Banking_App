using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using TransactionService.Infrastructure.Data;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Infrastructure.Logging;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Infrastructure.Redis;
using TransactionService.Models;
using TransactionService.Services;
using TransactionService.Services.Interface;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Swagger with authentication support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
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

builder.Services.AddSingleton<IRedisClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RedisClient>>();
    var host = builder.Configuration.GetValue<string>("REDIS_HOST") ?? "redis";
    var port = builder.Configuration.GetValue<string>("REDIS_PORT") ?? "6379";
    return new RedisClient(logger, $"{host}:{port}");
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
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ??
                        builder.Configuration["JWT:Issuer"] ??
                        "BankingApp";

        var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ??
                          builder.Configuration["JWT:Audience"] ??
                          "UserAccountAPI";

        var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ??
                     builder.Configuration["JWT:Key"] ??
                     "default-development-signing-key-min-16-chars";

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

// Helper method to configure HttpClient instances
static void ConfigureHttpClient(IServiceProvider services, HttpClient client, bool setTimeout = false)
{
    var configuration = services.GetRequiredService<IConfiguration>();

    var serviceToken = Environment.GetEnvironmentVariable("TRANSACTION_SERVICE_TOKEN") ??
                       configuration["ServiceAuthentication:Token"];

    var baseAddress = configuration["Services:UserAccountService"] ?? "http://user-account-service";

    if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException($"Invalid base address for HttpClient: {baseAddress}");
    }

    client.BaseAddress = uri;

    if (setTimeout)
    {
        client.Timeout = TimeSpan.FromSeconds(5);
    }

    if (!string.IsNullOrEmpty(serviceToken))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
    }
}

// Configure HttpClient for UserAccountClientService and map IUserAccountClient (typed client)
builder.Services.AddHttpClient<IUserAccountClient, UserAccountClientService>((services, client) =>
    ConfigureHttpClient(services, client));

// Configure HttpClient for TransactionValidator (named client)
builder.Services.AddHttpClient("UserAccountClient", (services, client) =>
    ConfigureHttpClient(services, client, setTimeout: true));

builder.Services.AddHttpClient("FraudDetectionClient", (services, client) =>
    ConfigureHttpClient(services, client, setTimeout: true));

builder.Services.AddHttpClient("FraudDetectionClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("Services:FraudDetectionService") ?? throw new InvalidOperationException());
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Register other required services
builder.Services.AddSingleton<ConcurrentDictionary<string, TaskCompletionSource<FraudResult>>>(_ =>
    new ConcurrentDictionary<string, TaskCompletionSource<FraudResult>>());
builder.Services.AddSingleton<ConcurrentDictionary<string, FraudResult>>(_ =>
    new ConcurrentDictionary<string, FraudResult>());
builder.Services.AddScoped<IFraudDetectionService, FraudDetectionService>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<TransactionValidator>();
builder.Services.AddSingleton<IRabbitMqClient, RabbitMqClient>();
builder.Services.AddScoped<ITransactionService, TransactionService.Services.TransactionService>();
builder.Services.AddSingleton<FallbackFraudService>();

// Define and register metrics
var requestsTotalCounter = Metrics.CreateCounter(
    "requests_total",
    "Total number of requests",
    new CounterConfiguration { LabelNames = new[] { "operation" } }
);
var successesTotalCounter = Metrics.CreateCounter(
    "successes_total",
    "Total number of successful operations",
    new CounterConfiguration { LabelNames = new[] { "operation" } }
);
var errorsTotalCounter = Metrics.CreateCounter(
    "errors_total",
    "Total number of failed operations",
    new CounterConfiguration { LabelNames = new[] { "operation" } }
);
var transactionDurationHistogram = Metrics.CreateHistogram(
    "transaction_duration_seconds",
    "Transaction processing duration in seconds",
    new HistogramConfiguration
    {
        LabelNames = new[] { "type" },
        Buckets = [0.1, 0.5, 1, 2, 5, 10]
    }
);

// Register metrics
builder.Services.AddSingleton(requestsTotalCounter);
builder.Services.AddSingleton(successesTotalCounter);
builder.Services.AddSingleton(errorsTotalCounter);
builder.Services.AddSingleton(transactionDurationHistogram);

var app = builder.Build();

// Validate the HttpClient configurations at startup (optional)
var loggerStartup = app.Services.GetRequiredService<ILogger<Program>>();
var httpClientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
var userAccountClient = httpClientFactory.CreateClient("UserAccountClient");
var fraudDetectionClient = httpClientFactory.CreateClient("FraudDetectionClient");
loggerStartup.LogInformation("UserAccountClient BaseAddress: {BaseAddress}",
    userAccountClient.BaseAddress?.ToString() ?? "Not set");
loggerStartup.LogInformation("FraudDetectionClient BaseAddress: {BaseAddress}",
    fraudDetectionClient.BaseAddress?.ToString() ?? "Not set");

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseDeveloperExceptionPage();
}

app.UseMetricServer();
app.UseHttpMetrics();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction API v1");
    c.RoutePrefix = string.Empty;
    c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
});

app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();