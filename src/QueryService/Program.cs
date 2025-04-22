using Nest;
using QueryService;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.



var settings = new ConnectionSettings(new Uri("http://localhost:9200"))
    .DefaultIndex("transactions");
var elasticClient = new ElasticClient(settings);



//builder.Services.AddSingleton<IElasticClient>(elasticClient);


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

using (var scope = app.Services.CreateScope())
{
    var rabbit = scope.ServiceProvider.GetRequiredService<RabbitMqConnection>();
    await rabbit.open_connection();
    await rabbit.open_channel();
    
    rabbit.send_message("CheckFraud", "Hello, RabbitMQ!");
}







app.UseHttpsRedirection();
app.MapGraphQL();

app.Run();
