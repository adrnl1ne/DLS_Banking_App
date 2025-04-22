using System.Text;
using RabbitMQ.Client;

namespace AccountService.Services;

public class RabbitMQEventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMQEventPublisher(IConfiguration configuration)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ_Host"],
            Port = configuration.GetValue<int>("RabbitMQ_Port"),
            UserName = configuration["RabbitMQ_Username"],
            Password = configuration["RabbitMQ_Password"]
        };

        Console.WriteLine(configuration["RabbitMQ_Host"]);

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
    }

    public void Publish(string queueName, string message)
    {
        _channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

        var body = Encoding.UTF8.GetBytes(message);
        _channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
