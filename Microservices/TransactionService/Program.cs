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
using TransactionService.Infrastructure.Messaging.RabbitMQ;
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
// Configure RabbitMQ
var rabbitMqConfig = new RabbitMqConfiguration
{
    HostName = builder.Configuration["RABBITMQ_HOST"] ?? "rabbitmq",
    Port = int.Parse(builder.Configuration["RABBITMQ_PORT"] ?? "5672"),
    UserName = builder.Configuration["RABBITMQ_USERNAME"] ?? "guest",
    Password = builder.Configuration["RABBITMQ_PASSWORD"] ?? "guest",
    VirtualHost = builder.Configuration["RABBITMQ_VHOST"] ?? "/",
};
builder.Services.AddSingleton(rabbitMqConfig);
builder.Services.AddSingleton<IRabbitMqClient, RabbitMqClient>();

// Configure Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration.GetValue<string>("JWT_ISSUER") ?? throw new InvalidOperationException("JWT_ISSUER must be configured"),
        ValidAudience = builder.Configuration.GetValue<string>("JWT_AUDIENCE") ?? throw new InvalidOperationException("JWT_AUDIENCE must be configured"),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration.GetValue<string>("JWT_KEY") ?? throw new InvalidOperationException("JWT_KEY must be configured")))
    };
});

// Configure HttpClient for User Account Service
var serviceToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0cmFuc2FjdGlvbi1zZXJ2aWNlIiwicm9sZSI6InNlcnZpY2UiLCJqdGkiOiJjNGEwMzRjYy1iMDE4LTQxYTYtOTNmMi02MDc5MDQ1MWU1OWEiLCJpc3MiOiJCYW5raW5nQXBwIiwic2NvcGVzIjpbInJlYWQ6YWNjb3VudHMiLCJ1cGRhdGU6YWNjb3VudC1iYWxhbmNlIl0sImV4cCI6MTc2MTI0NjM0NSwiYXVkIjoiVXNlckFjY291bnRBUEkifQ.xiE7sJOYZWizg-cvk_yKya4-vfaXUV9BDTXaJx5QgJE"
    ;
var userAccountServiceUrl = builder.Configuration["Services:UserAccountService"];
Console.WriteLine($"Configuring HttpClient for UserAccountClientService: URL={userAccountServiceUrl}, Token={serviceToken}");
if (string.IsNullOrWhiteSpace(userAccountServiceUrl) || !Uri.TryCreate(userAccountServiceUrl, UriKind.Absolute, out var uri))
{
    throw new InvalidOperationException($"Invalid or missing Services:UserAccountService URL: {userAccountServiceUrl ?? "NULL"}");
}
builder.Services.AddHttpClient<UserAccountClientService>(client =>
{
    client.BaseAddress = uri;
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddTransientHttpErrorPolicy(policyBuilder =>
    policyBuilder.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

// Register repositories
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

// Register services
builder.Services.AddScoped<ITransactionService, TransactionService.Services.TransactionService>();

// Create Prometheus metrics
var transactionCounter = Metrics.CreateCounter(
    "transactions_total",
    "Total number of transactions",
    new CounterConfiguration { LabelNames = ["type", "status"] }
);

var transactionDurationHistogram = Metrics.CreateHistogram(
    "transaction_duration_seconds",
    "Transaction processing duration in seconds",
    new HistogramConfiguration
    {
        LabelNames = ["type"],
        Buckets = [0.1, 0.5, 1, 2, 5, 10]
    }
);

// Register metrics
builder.Services.AddSingleton(transactionCounter);
builder.Services.AddSingleton(transactionDurationHistogram);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction API v1");
        c.RoutePrefix = string.Empty; // To serve the Swagger UI at the app's root
    });
}
else
{
    // Production configuration
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transaction API v1");
        c.RoutePrefix = "api-docs";
    });
}

app.UseMetricServer();
app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
