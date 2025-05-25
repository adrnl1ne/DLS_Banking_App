using System.Text;
using RabbitMQ.Client;
using UserAccountService.Infrastructure.Messaging;

namespace UserAccountService.Service;

public class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqEventPublisher(IConfiguration configuration)
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ_Host"] ?? "rabbitmq",
            Port = configuration.GetValue<int>("RabbitMQ_Port", 5672),
            UserName = configuration["RabbitMQ_Username"] ?? "guest",
            Password = configuration["RabbitMQ_Password"] ?? "guest"
        };

        Console.WriteLine($"Connecting to RabbitMQ: Host={factory.HostName}, Port={factory.Port}");
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare the banking.events topic exchange
        _channel.ExchangeDeclare(exchange: "banking.events", type: "topic", durable: true, autoDelete: false);
    }

    public void Publish(string routingKey, string message)
    {
        var body = Encoding.UTF8.GetBytes(message);
        var properties = _channel.CreateBasicProperties();
        properties.DeliveryMode = 2;

        _channel.BasicPublish(
            exchange: "banking.events",
            routingKey: routingKey,
            basicProperties: properties,
            body: body
        );
        Console.WriteLine($"Published event to banking.events with routing key: {routingKey}");
    }

    public void PublishToQueue(string queueName, string message)
    {
        try
        {
            // Declare the queue to make sure it exists
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            
            var body = Encoding.UTF8.GetBytes(message);
            var properties = _channel.CreateBasicProperties();
            properties.DeliveryMode = 2; // Make message persistent
            
            // Publish directly to the queue (empty exchange)
            _channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: properties,
                body: body);
            
            Console.WriteLine($"✅ PUBLISHED message to queue {queueName}: {message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ FAILED to publish to queue {queueName}: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}