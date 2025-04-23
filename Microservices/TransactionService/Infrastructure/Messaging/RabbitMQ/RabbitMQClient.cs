using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ;

public class RabbitMQClient : IRabbitMQClient, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQClient> _logger;

    // Constructor that accepts a configuration object
    public RabbitMQClient(IConfiguration configuration, ILogger<RabbitMQClient> logger)
    {
        _logger = logger;
        
        var host = configuration["RabbitMQ:Host"] ?? "rabbitmq";
        var port = configuration.GetValue<int>("RabbitMQ:Port", 5672);
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";
        var virtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = username,
            Password = password,
            VirtualHost = virtualHost
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", host, port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ at {Host}:{Port}", host, port);
            throw;
        }
    }

    // Constructor that accepts a RabbitMQConfiguration object
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
            _logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", config.HostName, config.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ at {Host}:{Port}", config.HostName, config.Port);
            throw;
        }
    }

    // Method to publish serialized messages
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
            
            _logger.LogInformation("Message published to queue {Queue}: {Message}", queue, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to {Queue}", queue);
            throw;
        }
    }

    // Method to publish string messages (used in new implementation)
    public void Publish(string queueName, string message)
    {
        try
        {
            _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            
            var body = Encoding.UTF8.GetBytes(message);
            
            _channel.BasicPublish(
                exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);
            
            _logger.LogInformation("Message published to queue {Queue}: {Message}", queueName, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to {Queue}", queueName);
            throw;
        }
    }

    // Subscribe to a queue with a handler callback
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
                    
                    _logger.LogInformation("Message received from queue {Queue}: {Message}", queue, json);
                    
                    var message = JsonSerializer.Deserialize<T>(json);
                    if (message != null)
                    {
                        handler(message);
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from {Queue}", queue);
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            _logger.LogInformation("Subscribed to queue {Queue}", queue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to {Queue}", queue);
            throw;
        }
    }

    // New async consumption method from the updated implementation
    public async Task<string> ConsumeAsync(string queueName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Setting up async consumption from queue {Queue}", queueName);
        
        _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);

        var tcs = new TaskCompletionSource<string>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.Token.Register(() => {
            _logger.LogInformation("Consumption from queue {Queue} was cancelled", queueName);
            tcs.TrySetCanceled();
        }, useSynchronizationContext: false);

        var consumer = new EventingBasicConsumer(_channel);
        string consumerTag = null;
        
        consumer.Received += (model, ea) =>
        {
            try {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                _logger.LogInformation("Message received from queue {Queue} during async consumption: {Message}", queueName, message);
                
                tcs.TrySetResult(message);
                _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                
                // Cancel consumption after receiving the message
                if (consumerTag != null) {
                    _channel.BasicCancel(consumerTag);
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error processing message from {Queue} during async consumption", queueName);
                _channel.BasicNack(ea.DeliveryTag, false, true);
                tcs.TrySetException(ex);
            }
        };

        consumerTag = _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

        return await tcs.Task;
    }

    public void Dispose()
    {
        try
        {
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
            _logger.LogInformation("RabbitMQ connection closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RabbitMQ client disposal");
        }
    }
}
