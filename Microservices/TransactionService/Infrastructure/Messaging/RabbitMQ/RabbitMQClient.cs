using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public class RabbitMQClient(
        ILogger<RabbitMQClient> logger,
        string hostName = "rabbitmq",
        int port = 5672,
        string userName = "guest",
        string password = "guest")
        : IRabbitMqClient, IDisposable
    {
        private IConnection? _connection;
        private IModel? _channel;
        private readonly object _connectionLock = new object();
        private bool _disposed;

        public bool IsConnected => _connection?.IsOpen == true && _channel?.IsOpen == true && !_disposed;

        public void EnsureConnection()
        {
            if (IsConnected)
                return;

            lock (_connectionLock)
            {
                if (IsConnected)
                    return;

                logger.LogInformation("Establishing connection to RabbitMQ at {Host}:{Port}", hostName, port);

                try
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = hostName,
                        Port = port,
                        UserName = userName,
                        Password = password,
                        RequestedHeartbeat = TimeSpan.FromSeconds(30),
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                    };

                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();

                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    logger.LogInformation("Successfully connected to RabbitMQ at {Host}:{Port}", hostName, port);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to connect to RabbitMQ");
                    CleanupConnection();
                    throw;
                }
            }
        }

        public void Publish(string queueName, string message)
        {
            try
            {
                EnsureConnection();
                
                // Create fresh channel for each publish to avoid "Already closed" issues
                using var channel = _connection!.CreateModel();
                
                // First declare the queue without passive flag to create it if it doesn't exist
                try
                {
                    channel.QueueDeclare(
                        queue: queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null
                    );
                    logger.LogInformation("Queue '{QueueName}' declared or confirmed", queueName);
                }
                catch (OperationInterruptedException ex) when (ex.Message.Contains("PRECONDITION_FAILED"))
                {
                    // This happens when queue exists with different settings
                    logger.LogWarning("Queue '{QueueName}' exists with different settings, using existing queue.", queueName);
                }
                
                var body = Encoding.UTF8.GetBytes(message);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.DeliveryMode = 2; // Persistent
                properties.ContentType = "application/json";
                
                // Publish to the queue
                channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);
                
                logger.LogInformation("Published message to queue '{QueueName}': {MessageLength} bytes",
                    queueName, body.Length);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing message to queue: {QueueName}", queueName);
                
                // Recreate the connection to get a fresh state - important for recovery
                CleanupConnection();
                throw;
            }
        }

        public void Subscribe(string queueName, Action<string> handler)
        {
            try
            {
                EnsureConnection();
                
                // Create fresh channel for each subscription
                var subscriptionChannel = _connection!.CreateModel();
                
                // Declare the queue - non-passive to create if needed
                subscriptionChannel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );
                
                logger.LogInformation("Created or confirmed queue for subscription: {QueueName}", queueName);
                
                // Set QoS to avoid overwhelming consumers
                subscriptionChannel.BasicQos(0, 1, false);
                
                var consumer = new EventingBasicConsumer(subscriptionChannel);
                consumer.Received += (sender, ea) =>
                {
                    string body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    logger.LogInformation("Received message from queue '{QueueName}': {MessageLength} bytes",
                        queueName, ea.Body.Length);
                    
                    try
                    {
                        handler(body);
                        subscriptionChannel.BasicAck(ea.DeliveryTag, false);
                        logger.LogInformation("Successfully processed message from queue '{QueueName}'", queueName);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing message from queue: {QueueName}", queueName);
                        subscriptionChannel.BasicNack(ea.DeliveryTag, false, true); // Requeue
                    }
                };
                
                subscriptionChannel.BasicConsume(
                    queue: queueName,
                    autoAck: false, 
                    consumer: consumer);
                    
                logger.LogInformation("Successfully subscribed to queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error subscribing to queue: {QueueName}", queueName);
                
                // Reset connection state
                _channel = null;
                _connection = null;
                throw;
            }
        }



        public void Subscribe<T>(string queueName, Func<T, Task<bool>> handler) where T : class
        {
            Subscribe(queueName, async message =>
            {
                try
                {
                    T? deserializedMessage;
                    try
                    {
                        // Try to deserialize as JSON object
                        deserializedMessage = JsonSerializer.Deserialize<T>(message);
                    }
                    catch
                    {
                        // If that fails and T is string, try direct casting
                        if (typeof(T) == typeof(string))
                        {
                            deserializedMessage = message as T;
                        }
                        else
                        {
                            // For other types, try case-by-case handling
                            if (typeof(T) == typeof(object))
                            {
                                deserializedMessage = message as T;
                            }
                            else
                            {
                                logger.LogError("Failed to deserialize message to type {Type}", typeof(T).Name);
                                return;
                            }
                        }
                    }

                    if (deserializedMessage != null)
                    {
                        await handler(deserializedMessage);
                        return;
                    }
                    
                    logger.LogError("Failed to deserialize message to type {Type}", typeof(T).Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing message");
                }
            });
        }

        // Implement the DeclareQueue method in the RabbitMQClient class
        public void DeclareQueue(string queueName, bool durable = true, bool exclusive = false, bool autoDelete = false)
        {
            try
            {
                EnsureConnection();
                
                // Create fresh channel for declaring the queue to avoid "Already closed" issues
                using var freshChannel = _connection!.CreateModel();
                
                try
                {
                    // Directly declare the queue - this will create it if it doesn't exist
                    // or confirm it if it already exists with the same settings
                    freshChannel.QueueDeclare(
                        queue: queueName,
                        durable: durable,
                        exclusive: exclusive,
                        autoDelete: autoDelete,
                        arguments: null);
                    logger.LogInformation("Queue '{QueueName}' declared successfully with durable={Durable}",
                        queueName, durable);
                }
                catch (OperationInterruptedException ex) when (ex.Message.Contains("PRECONDITION_FAILED"))
                {
                    // This happens when queue exists with different settings
                    logger.LogWarning("Queue '{QueueName}' exists with different settings. Using existing queue.", queueName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to declare queue {QueueName}", queueName);
                CleanupConnection();
                throw;
            }
        }

        // Add this implementation of the missing CreateChannel method
        public IModel CreateChannel()
        {
            EnsureConnection();
            return _connection!.CreateModel();
        }

        private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
        {
            logger.LogWarning("RabbitMQ connection shutdown. Reason: {Reason}", e.ReplyText);
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
                logger.LogError(ex, "Error disposing RabbitMQ connections");
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