using System.Net.Http.Headers;
using Nest;
using QueryService;
using QueryService.utils;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

static void ConfigureHttpClient(IServiceProvider services, HttpClient client, bool setTimeout = false)
{
    var configuration = services.GetRequiredService<IConfiguration>();

    var serviceToken = Environment.GetEnvironmentVariable("QUERY_SERVICE_TOKEN") ??
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

// Register HttpClient with proper configuration
builder.Services.AddHttpClient("ServiceClient", (sp, client) => {
    ConfigureHttpClient(sp, client);
});

builder.Services.AddSingleton<IElasticClient>(sp =>
{
    var settings = new ConnectionSettings(new Uri("http://elasticsearch:9200"))
        .DefaultIndex("transactions");
    return new ElasticClient(settings);
});


// Register GraphQL
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", corsPolicyBuilder =>
    {
        corsPolicyBuilder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddHostedService<RabbitMqListener>();

builder.Services.AddSingleton<RabbitMqConnection>(sp =>
{
    var config = builder.Configuration;
    var hostName = config["RabbitMQ:HostName"] ?? "localhost";
    var userName = config["RabbitMQ:UserName"] ?? "guest";
    var password = config["RabbitMQ:Password"] ?? "guest";
    return new RabbitMqConnection(hostName, userName, password);
});

var app = builder.Build();

await Helpers.EnsureElasticsearchIndicesAsync(app.Services);

// Attempt to fix existing index mappings
await Helpers.UpdateExistingIndex(app.Services, "account_events");

// Add this for initial data load
await Helpers.LoadInitialAccountDataAsync(app.Services);

app.UseCors("AllowAll");
app.MapGraphQL();
app.MapHealthChecks("/health");

app.Run();