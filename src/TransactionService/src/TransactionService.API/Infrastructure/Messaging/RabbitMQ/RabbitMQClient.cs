using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TransactionService.API.Infrastructure.Messaging.RabbitMQ;

public class RabbitMQClient : IRabbitMQClient, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQClient> _logger;

    public RabbitMQClient(RabbitMQConfiguration config, ILogger<RabbitMQClient> logger)
    {
        _logger = logger;
        
        var factory = new ConnectionFactory
        {
            HostName = config.HostName,
            Port = config.Port,
            UserName = config.UserName,
            Password = config.Password,
            VirtualHost = config.VirtualHost
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _logger.LogInformation("Connected to RabbitMQ");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    public void PublishMessage<T>(string queue, T message)
    {
        try
        {
            _channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false);
            
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            _channel.BasicPublish(
                exchange: "",
                routingKey: queue,
                basicProperties: null,
                body: body);
            
            _logger.LogInformation($"Message published to queue {queue}: {json}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish message to {queue}");
            throw;
        }
    }

    public void SubscribeToQueue<T>(string queue, Action<T> handler)
    {
        try
        {
            _channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var json = Encoding.UTF8.GetString(body);
                    
                    _logger.LogInformation($"Message received from queue {queue}: {json}");
                    
                    var message = JsonSerializer.Deserialize<T>(json);
                    if (message != null)
                    {
                        handler(message);
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing message from {queue}");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            _logger.LogInformation($"Subscribed to queue {queue}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to subscribe to {queue}");
            throw;
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}