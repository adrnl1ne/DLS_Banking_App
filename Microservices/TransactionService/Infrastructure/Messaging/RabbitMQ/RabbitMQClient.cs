using System;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ;

public class RabbitMqClient : IRabbitMqClient, IDisposable
{
    private readonly ILogger<RabbitMqClient> _logger;
    private readonly string _hostName;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private IConnection? _connection;
    private IModel? _channel;
    private bool _initialized;
    private bool _disposed;
    private readonly object _channelLock = new object();

    public RabbitMqClient(ILogger<RabbitMqClient> logger, string hostName = "rabbitmq", int port = 5672, string username = "guest", string password = "guest")
    {
        _logger = logger;
        _hostName = hostName;
        _port = port;
        _username = username;
        _password = password;
        
        InitializeConnection();
    }

    private void InitializeConnection()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _hostName,
                Port = _port,
                UserName = _username,
                Password = _password,
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            // Create all necessary queues on startup
            _channel.QueueDeclare(queue: "CheckFraud", durable: false, exclusive: false, autoDelete: false);
            _channel.QueueDeclare(queue: "TransactionServiceQueue", durable: false, exclusive: false, autoDelete: false);
            _channel.QueueDeclare(queue: "FraudResult", durable: false, exclusive: false, autoDelete: false);
            _channel.QueueDeclare(queue: "FraudEvents", durable: false, exclusive: false, autoDelete: false);
            
            _logger.LogInformation("RabbitMQ client initialized successfully. Connected to {Host}:{Port}", _hostName, _port);
            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ client");
            _initialized = false;
        }
    }

    public void Publish(string queueName, string message)
    {
        try
        {
            lock (_channelLock)
            {
                // Ensure connection and channel are available
                EnsureConnection();
                
                _logger.LogInformation("Declaring queue {QueueName}", queueName);
                _channel.QueueDeclare(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
                
                var body = Encoding.UTF8.GetBytes(message);
                
                // Add delivery confirmation
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true; // Make message persistent
                
                // Publish with the persistent flag
                _channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);
                _logger.LogInformation("Published message to {QueueName}: {Message}", queueName, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to {QueueName}", queueName);
            
            // Try to reconnect if needed
            try
            {
                CloseConnection();
                EnsureConnection();
                _logger.LogInformation("Reconnected to RabbitMQ after publish failure");
            }
            catch (Exception reconnectEx)
            {
                _logger.LogError(reconnectEx, "Failed to reconnect to RabbitMQ");
            }
            
            throw; // Re-throw the exception to notify the caller
        }
    }

    public void Subscribe(string queue, Action<string> callback)
    {
        if (!_initialized)
        {
            _logger.LogWarning("RabbitMQ client not initialized. Cannot subscribe to {Queue}", queue);
            // Try to reconnect
            InitializeConnection();
            if (!_initialized)
            {
                return;
            }
        }

        try
        {
            _channel!.QueueDeclare(queue: queue, durable: false, exclusive: false, autoDelete: false);
            _logger.LogInformation("Subscribed to queue {Queue}", queue);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Received message from {Queue}: {Message}", queue, message);

                try
                {
                    callback(message);
                    _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message from {Queue}: {Message}", queue, message);
                    _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            _channel.BasicConsume(queue: queue,
                autoAck: false,
                consumer: consumer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to {Queue}", queue);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            if (_channel != null)
            {
                _channel.Close();
                _channel.Dispose();
            }

            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
            }
        }

        _disposed = true;
    }

    private void EnsureConnection()
    {
        if (_connection == null || !_connection.IsOpen)
        {
            InitializeConnection();
        }
    }

    private void CloseConnection()
    {
        if (_connection != null && _connection.IsOpen)
        {
            _connection.Close();
        }
    }
}
