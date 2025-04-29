using Nest;
using QueryService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<IElasticClient>(sp =>
{
    var settings = new ConnectionSettings(new Uri("http://localhost:9200"))
        .DefaultIndex("transactions");

    return new ElasticClient(settings);
});

//Swagger
// Add services to the container.
builder.Services.AddControllers();

// Add Swagger here
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHostedService<RabbitMqListener>();

builder.Services.AddSingleton<RabbitMqConnection>(sp =>
{
    var config = builder.Configuration;
    var hostName = "rabbitmq";//config["RabbitMQ:HostName"];
    var userName = config["RabbitMQ:UserName"];
    var password = config["RabbitMQ:Password"];
    
    return new RabbitMqConnection(hostName, userName, password);
});

builder.Services.AddHostedService(provider =>
    new RabbitMqListener(
        provider.GetRequiredService<RabbitMqConnection>(),
        provider.GetRequiredService<IElasticClient>()
    ));

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>();

var app = builder.Build();



app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapGraphQL();

app.Run();
