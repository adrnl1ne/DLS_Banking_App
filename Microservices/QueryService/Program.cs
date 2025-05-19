using Nest;
using QueryService;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

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
    var hostName = config["RabbitMQ:HostName"];
    var userName = config["RabbitMQ:UserName"];
    var password = config["RabbitMQ:Password"];
    return new RabbitMqConnection(hostName, userName, password);
});

var app = builder.Build();

await Helpers.EnsureElasticsearchIndicesAsync(app.Services);

app.UseCors("AllowAll");

app.MapGraphQL();
app.MapHealthChecks("/health");

app.Run();