using Nest;
using QueryService;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

// Add services to the container.
builder.Services.AddSingleton<IElasticClient>(sp =>
{
    var settings = new ConnectionSettings(new Uri("http://elasticsearch:9200"))
        .DefaultIndex("transactions");

    return new ElasticClient(settings);
});

//Swagger
// Add services to the container.
builder.Services.AddControllers();

// Add Swagger here
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>();

var app = builder.Build();

await Helpers.EnsureElasticsearchIndicesAsync(app.Services);

app.MapControllers();



if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapGraphQL();
app.MapHealthChecks("/health");

app.Run();
