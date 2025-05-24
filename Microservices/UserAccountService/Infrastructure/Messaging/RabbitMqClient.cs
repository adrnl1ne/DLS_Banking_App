using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace UserAccountService.Infrastructure.Messaging
{
    public class RabbitMqClient : IRabbitMqClient
    {
        private readonly ILogger<RabbitMqClient> _logger;
        private readonly string _hostName;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private IConnection? _connection;
        private IModel? _channel;
        private bool _disposed;
        private bool _isConnected;

        private readonly object _connectionLock = new();

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
            _isConnected = false;

            // Log connection parameters at startup
            _logger.LogInformation("Connecting to RabbitMQ: Host={Host}, Port={Port}", hostName, port);
        }

        public bool IsConnected => _connection?.IsOpen == true && _channel?.IsOpen == true && !_disposed;

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
                    CleanupConnection(); // Clean up any existing connection

                    var factory = new ConnectionFactory
                    {
                        HostName = _hostName,
                        Port = _port,
                        UserName = _username,
                        Password = _password,
                        RequestedHeartbeat = TimeSpan.FromSeconds(30),
                        DispatchConsumersAsync = true, // Enable async consumer dispatch for AsyncEventingConsumer
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                    };

                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();
                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _isConnected = true;
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

        public IModel CreateChannel()
        {
            EnsureConnection();
            return _connection!.CreateModel();
        }

        public void Publish<T>(string queueName, T message) where T : class
        {
            EnsureConnection();

            try
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);

                // Ensure the queue exists before publishing
                _channel!.QueueDeclare(
                    queue: queueName,
                    durable: true, // Make sure queue is durable
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                _logger.LogDebug("Queue {QueueName} declared or confirmed", queueName);

                // Add these properties to your messages
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.DeliveryMode = 2; // Persistent
                properties.ContentType = "application/json";
                properties.Type = message.GetType().Name; // Message type for clarity
                properties.MessageId = Guid.NewGuid().ToString(); // Unique ID for traceability
                properties.AppId = "UserAccountService"; // Service name for visibility
                properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

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

        public void EnsureQueueExists(string queueName, bool durable)
        {
            try
            {
                // Make sure we have a connection first
                EnsureConnection();

                // Always create a fresh channel for queue declarations
                using var freshChannel = _connection!.CreateModel();

                _logger.LogInformation("Ensuring queue {QueueName} exists with durable={Durable}", queueName, durable);

                // Never use passive declaration for queue creation - it will fail if queue doesn't exist
                freshChannel.QueueDeclare(
                    queue: queueName,
                    durable: durable,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                _logger.LogInformation("Queue {QueueName} exists or was created with durable={Durable}", queueName, durable);
            }
            catch (OperationInterruptedException ex) when (ex.Message.Contains("PRECONDITION_FAILED"))
            {
                // This happens when queue exists with different settings
                _logger.LogWarning("Queue {QueueName} exists with different settings. Deleting and recreating with durable={Durable}", queueName, durable);

                try
                {
                    // Try to delete and recreate with correct settings
                    using var freshChannel = _connection!.CreateModel();
                    freshChannel.QueueDelete(queueName);
                    freshChannel.QueueDeclare(
                        queue: queueName,
                        durable: durable,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);
                    _logger.LogInformation("Queue {QueueName} recreated with durable={Durable}", queueName, durable);
                }
                catch (Exception recreateEx)
                {
                    _logger.LogError(recreateEx, "Failed to recreate queue {QueueName}", queueName);
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure queue exists: {QueueName}", queueName);

                // Clean up and reconnect to get a fresh connection
                CleanupConnection();
                throw;
            }
        }

        public void Subscribe<T>(string queueName, Func<T, Task<bool>> handler) where T : class
        {
            // Just delegate to the async version since they do the same thing
            SubscribeAsync<T>(queueName, handler);
        }

        public void SubscribeAsync<T>(string queueName, Func<T, Task<bool>> handler) where T : class
        {
            try
            {
                // Make sure we have a connection
                EnsureConnection();

                // Create a fresh channel for this subscription
                var channel = _connection!.CreateModel();

                // Always declare the queue when subscribing - don't use passive declaration
                try
                {
                    channel.QueueDeclare(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    _logger.LogInformation("Created or confirmed queue for subscription: {QueueName}", queueName);
                }
                catch (OperationInterruptedException ex) when (ex.Message.Contains("PRECONDITION_FAILED"))
                {
                    // If the queue exists but with different settings, just use it as is
                    _logger.LogWarning("Queue {QueueName} exists with different settings. Using existing queue.", queueName);
                }

                // Set prefetch count to 1 to process messages one at a time
                channel.BasicQos(0, 1, false);
                _logger.LogDebug("Set QoS prefetch to 1 for queue {QueueName}", queueName);

                // Create an async consumer to handle messages
                var consumer = new AsyncEventingBasicConsumer(channel);

                // Set up the Received event handler to process messages asynchronously
                consumer.Received += async (sender, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var deliveryTag = ea.DeliveryTag;

                    _logger.LogInformation("Received message from {QueueName}: {Length} bytes",
                        queueName, body.Length);
                    _logger.LogDebug("Message payload: {Message}", message);

                    try
                    {
                        // Attempt to deserialize the message
                        var deserializedMessage = JsonSerializer.Deserialize<T>(
                            message,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (deserializedMessage == null)
                        {
                            _logger.LogError("Failed to deserialize message: {Message}", message);
                            // Reject the message and don't requeue if we can't deserialize it
                            channel.BasicNack(deliveryTag, false, false);
                            return;
                        }

                        // Log detail about the message before processing
                        _logger.LogInformation("Processing message of type {MessageType}", typeof(T).Name);

                        // Process the message with the handler
                        bool success = await handler(deserializedMessage);

                        if (success)
                        {
                            // Acknowledge successful processing
                            channel.BasicAck(deliveryTag, false);
                            _logger.LogInformation("Successfully processed message from {QueueName}", queueName);
                        }
                        else
                        {
                            // Reject and requeue for retry if processing failed but is retriable
                            channel.BasicNack(deliveryTag, false, true);
                            _logger.LogWarning("Failed to process message from {QueueName}, requeueing", queueName);
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "JSON deserialization error for message: {Message}", message);
                        // Don't requeue messages with JSON errors as they'll never deserialize correctly
                        channel.BasicNack(deliveryTag, false, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from {QueueName}: {Message}", queueName, message);
                        // Reject and requeue on unexpected error
                        channel.BasicNack(deliveryTag, false, true);
                    }
                };

                // Start consuming messages with explicit acknowledgement
                channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("Successfully subscribed to queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to {QueueName}", queueName);
                _isConnected = false;
                CleanupConnection();
                throw;
            }
        }

        private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            _isConnected = false;
            _logger.LogWarning("RabbitMQ connection shutdown. Reason: {Reason}", e.ReplyText);
        }

        private void CleanupConnection()
        {
            try
            {
                _channel?.Close();
                _channel?.Dispose();
                _connection?.Close();
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
                _isConnected = false;

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