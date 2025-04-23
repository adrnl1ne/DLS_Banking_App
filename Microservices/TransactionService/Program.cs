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
using Prometheus;
using TransactionService.Infrastructure.Data;
using TransactionService.Infrastructure.Data.Repositories;
using TransactionService.Infrastructure.Messaging.RabbitMQ;
using TransactionService.Services;
using TransactionService.Clients;

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

// Configure DB Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TransactionDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

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
        ValidIssuer = builder.Configuration.GetValue<string>("Jwt:Issuer", ""),
        ValidAudience = builder.Configuration.GetValue<string>("Jwt:Audience", ""), 
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration.GetValue<string>("Jwt:Key", "")))
    };
});

// Configure HttpClient for User Account Service
builder.Services.AddHttpClient<UserAccountClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:UserAccountService"]);
});

// Register RabbitMQ
builder.Services.AddSingleton<IRabbitMQClient, RabbitMQClient>();

// Register repositories
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();

// Register services
builder.Services.AddScoped<ITransactionService, TransactionService.Services.TransactionService>();

// Create Prometheus metrics
var transactionCounter = Metrics.CreateCounter(
    "transactions_total",
    "Total number of transactions",
    new CounterConfiguration { LabelNames = new[] { "type", "status" } }
);

var transactionDurationHistogram = Metrics.CreateHistogram(
    "transaction_duration_seconds",
    "Transaction processing duration in seconds",
    new HistogramConfiguration
    {
        LabelNames = new[] { "type" },
        Buckets = new double[] { 0.1, 0.5, 1, 2, 5, 10 }
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

app.UseHttpsRedirection();

// Add Prometheus metrics
app.UseMetricServer();
app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
