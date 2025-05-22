using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace UserAccountService.Infrastructure.Messaging
{
    public class RabbitMqClient : IRabbitMqClient, IDisposable
    {
        private readonly ILogger<RabbitMqClient> _logger;
        private readonly string _hostName;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private IConnection? _connection;
        private IModel? _channel;
        private bool _disposed;
        private readonly object _connectionLock = new object();

        public bool IsConnected => _connection?.IsOpen == true && _channel?.IsOpen == true && !_disposed;

        public RabbitMqClient(
            ILogger<RabbitMqClient> logger,
            string hostName = "rabbitmq",
            int port = 5672,
            string username = "guest",
            string password = "guest")
        {
            _logger = logger;
            _hostName = hostName;
            _port = port;
            _username = username;
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
                        UserName = _username,
                        Password = _password,
                        RequestedHeartbeat = TimeSpan.FromSeconds(30),
                        DispatchConsumersAsync = true,
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

        public void Publish<T>(string queueName, T message) where T : class
        {
            EnsureConnection();

            try
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                // Use passive declare to check if queue exists
                try
                {
                    _channel!.QueueDeclarePassive(queueName);
                    _logger.LogDebug("Using existing queue for publishing: {QueueName}", queueName);
                }
                catch (Exception)
                {
                    // Queue doesn't exist, create it as non-durable to match existing setup
                    _channel!.QueueDeclare(
                        queue: queueName,
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);
                    _logger.LogInformation("Created queue for publishing: {QueueName}", queueName);
                }

                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.DeliveryMode = 2; // Persistent

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

        public void Subscribe<T>(string queueName, Func<T, Task<bool>> handler) where T : class
        {
            EnsureConnection();

            try
            {
                // Use passive declare to check if queue exists
                try
                {
                    _channel!.QueueDeclarePassive(queueName);
                    _logger.LogDebug("Using existing queue for subscription: {QueueName}", queueName);
                }
                catch (Exception)
                {
                    // Queue doesn't exist, create it as non-durable to match existing setup
                    _channel!.QueueDeclare(
                        queue: queueName,
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);
                    _logger.LogInformation("Created queue for subscription: {QueueName}", queueName);
                }

                // Set prefetch count to 1 to ensure one message is processed at a time
                _channel!.BasicQos(0, 1, false);

                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.Received += async (sender, ea) =>
                {
                    string body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    bool success = false;
                    T? message = default;

                    try
                    {
                        message = JsonSerializer.Deserialize<T>(body);
                        if (message != null)
                        {
                            // Process the message
                            success = await handler(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue: {QueueName}", queueName);
                    }
                    finally
                    {
                        try
                        {
                            // Acknowledge or reject the message based on processing result
                            if (success)
                            {
                                _channel.BasicAck(ea.DeliveryTag, false);
                                _logger.LogDebug("Message acknowledged in queue: {QueueName}", queueName);
                            }
                            else
                            {
                                // Requeue the message
                                _channel.BasicNack(ea.DeliveryTag, false, true);
                                _logger.LogWarning("Message rejected and requeued in queue: {QueueName}", queueName);
                            }
                        }
                        catch (Exception ackEx)
                        {
                            _logger.LogError(ackEx, "Error during message ack/nack in queue: {QueueName}", queueName);
                        }
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