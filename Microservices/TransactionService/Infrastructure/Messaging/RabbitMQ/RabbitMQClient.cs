using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions; // Add this import for RabbitMQ exceptions

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public class RabbitMQClient : IRabbitMqClient, IDisposable
    {
        private readonly ILogger<RabbitMQClient> _logger;
        private readonly string _hostName;
        private readonly int _port;
        private readonly string _userName;
        private readonly string _password;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly object _connectionLock = new object();
        private bool _disposed;

        public bool IsConnected => _connection?.IsOpen == true && _channel?.IsOpen == true && !_disposed;

        public RabbitMQClient(
            ILogger<RabbitMQClient> logger,
            string hostName = "rabbitmq",
            int port = 5672,
            string userName = "guest",
            string password = "guest")
        {
            _logger = logger;
            _hostName = hostName;
            _port = port;
            _userName = userName;
            _password = password;
        }

        public void EnsureConnection()
        {
            if (IsConnected)
                return;

            lock (_connectionLock)
            {
                if (IsConnected)
                    return;

                _logger.LogInformation("Establishing connection to RabbitMQ at {Host}:{Port}", _hostName, _port);
                
                try
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = _hostName,
                        Port = _port,
                        UserName = _userName,
                        Password = _password,
                        RequestedHeartbeat = TimeSpan.FromSeconds(30),
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                    };

                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();
                    
                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _logger.LogInformation("Successfully connected to RabbitMQ at {Host}:{Port}", _hostName, _port);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to RabbitMQ");
                    CleanupConnection();
                    throw;
                }
            }
        }

        public void Publish(string queueName, string message)
        {
            EnsureConnection();
            
            try
            {
                // First try to use passive declaration to check if queue exists without changing properties
                try
                {
                    _channel!.QueueDeclarePassive(queueName);
                    _logger.LogInformation("Using existing queue: {QueueName}", queueName);
                }
                catch (Exception)
                {
                    // Queue doesn't exist yet, create it with desired parameters
                    try
                    {
                        // Try first with durable
                        _channel!.QueueDeclare(queue: queueName,
                            durable: true,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);
                        _logger.LogInformation("Created durable queue: {QueueName}", queueName);
                    }
                    catch (OperationInterruptedException ex) when (ex.Message.Contains("inequivalent arg 'durable'"))
                    {
                        // Change this line - use the correct namespace
                        // If that fails with durable mismatch, try with non-durable
                        _channel!.QueueDeclare(queue: queueName,
                            durable: false,
                            exclusive: false,
                            autoDelete: false,
                            arguments: null);
                        _logger.LogInformation("Created non-durable queue: {QueueName}", queueName);
                    }
                }

                var body = Encoding.UTF8.GetBytes(message);
                
                // Create message properties
                var properties = _channel!.CreateBasicProperties();
                properties.Persistent = true; // Make message persistent regardless of queue durability
                
                // Publish the message
                _channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);
                
                _logger.LogInformation("Published message to queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to queue: {QueueName}", queueName);
                throw;
            }
        }

        public void Subscribe(string queueName, Action<string> handler)
        {
            EnsureConnection();

            try
            {
                // Try to use existing queue
                try
                {
                    _channel!.QueueDeclarePassive(queueName);
                    _logger.LogInformation("Using existing queue for subscription: {QueueName}", queueName);
                }
                catch (Exception)
                {
                    // If it doesn't exist, create it (using non-durable to match existing setup)
                    _channel!.QueueDeclare(
                        queue: queueName,
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);
                    _logger.LogInformation("Created queue for subscription: {QueueName}", queueName);
                }

                // Set prefetch count to 1 to ensure one message is processed at a time
                _channel.BasicQos(0, 1, false);

                var consumer = new EventingBasicConsumer(_channel);

                consumer.Received += (sender, ea) =>
                {
                    string body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    
                    try
                    {
                        handler(body);
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue: {QueueName}", queueName);
                        _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue
                    }
                };

                _channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("Successfully subscribed to queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to queue: {QueueName}", queueName);
                throw;
            }
        }

        public void Subscribe<T>(string queueName, Func<T, Task<bool>> handler) where T : class
        {
            EnsureConnection();

            try
            {
                // CHANGE THIS SECTION: Always declare queue (don't use passive)
                _channel!.QueueDeclare(
                    queue: queueName,
                    durable: true,         // Make it persistent
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                
                _logger.LogInformation("Created or confirmed queue for subscription: {QueueName}", queueName);

                // Set prefetch count to 1 to ensure one message is processed at a time
                _channel.BasicQos(0, 1, false);

                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.Received += async (sender, ea) =>
                {
                    string body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    bool success = false;

                    try
                    {
                        var message = JsonSerializer.Deserialize<T>(body);
                        if (message != null)
                        {
                            // Process the message
                            success = await handler(message);
                        }

                        if (success)
                        {
                            _channel.BasicAck(ea.DeliveryTag, false);
                        }
                        else
                        {
                            // Requeue the message
                            _channel.BasicNack(ea.DeliveryTag, false, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue: {QueueName}", queueName);
                        _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue
                    }
                };

                _channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("Successfully subscribed to queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to queue: {QueueName}", queueName);
                throw;
            }
        }

        private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shutdown. Reason: {Reason}", e.ReplyText);
            CleanupConnection();
        }

        private void CleanupConnection()
        {
            try
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing RabbitMQ connections");
            }
            finally
            {
                _channel = null;
                _connection = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CleanupConnection();
            GC.SuppressFinalize(this);
        }
    }
}
