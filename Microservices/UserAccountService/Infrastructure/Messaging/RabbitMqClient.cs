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
    public class RabbitMqClient : IRabbitMqClient, IDisposable
    {
        private readonly ILogger<RabbitMqClient> _logger;
        private readonly string _hostName;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private IConnection _connection;
        private IModel _channel;
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
            
            EnsureConnection();
        }
        
        public void EnsureConnection()
        {
            if (IsConnected)
                return;
                
            lock (_connectionLock)
            {
                if (IsConnected)
                    return;
                    
                if (_connection != null)
                {
                    try 
                    { 
                        _connection.Dispose(); 
                        _connection = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing existing connection");
                    }
                }
                    
                if (_channel != null)
                {
                    try 
                    { 
                        _channel.Dispose(); 
                        _channel = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error disposing existing channel");
                    }
                }
                    
                try
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = _hostName,
                        Port = _port,
                        UserName = _username,
                        Password = _password,
                        RequestedHeartbeat = TimeSpan.FromSeconds(30),
                        DispatchConsumersAsync = false, // Using sync consumer for simplicity
                        AutomaticRecoveryEnabled = true,
                        NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                    };
                    
                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();
                    
                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _logger.LogInformation("RabbitMQ connection established to {Host}:{Port}", _hostName, _port);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not connect to RabbitMQ");
                    throw;
                }
            }
        }

        public void Publish<T>(string queueName, T message) where T : class
        {
            string messageJson = JsonSerializer.Serialize(message);
            Publish(queueName, messageJson);
        }

        public void Publish(string queueName, string messageJson)
        {
            EnsureConnection();
            
            try
            {
                // Use passive declaration to check if queue exists with current settings
                try
                {
                    _channel.QueueDeclarePassive(queueName);
                    _logger.LogDebug("Queue {QueueName} exists", queueName);
                }
                catch
                {
                    // Queue doesn't exist or differs in properties, so create it
                    _logger.LogInformation("Declaring queue {QueueName}", queueName);
                    _channel.QueueDeclare(
                        queue: queueName,
                        durable: true, // Make queue persistent
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);
                }
                
                var body = Encoding.UTF8.GetBytes(messageJson);
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;  // Make messages persistent
                properties.DeliveryMode = 2;   // Make messages persistent (2 = persistent)
                
                _channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);
                
                _logger.LogInformation("Message published to queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing message to {QueueName}", queueName);
                throw;
            }
        }
        
        public void Subscribe<T>(string queueName, Func<T, Task<bool>> handler) where T : class
        {
            // Adapt the generic method to call the non-generic one
            Subscribe(queueName, messageJson => 
            {
                try
                {
                    var message = JsonSerializer.Deserialize<T>(messageJson);
                    if (message != null)
                    {
                        Task.Run(async () => 
                        {
                            try
                            {
                                await handler(message);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error in message handler");
                                throw;
                            }
                        }).Wait();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing message");
                    throw;
                }
            });
        }

        public void Subscribe(string queueName, Action<string> handler)
        {
            EnsureConnection();
            
            try
            {
                // Use passive declaration to check if queue exists with current settings
                try
                {
                    _channel.QueueDeclarePassive(queueName);
                    _logger.LogDebug("Queue {QueueName} exists", queueName);
                }
                catch
                {
                    // Queue doesn't exist or differs in properties, so create it
                    _logger.LogInformation("Declaring queue {QueueName}", queueName);
                    _channel.QueueDeclare(
                        queue: queueName,
                        durable: true, // Make queue persistent
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);
                }
                
                // Set prefetch count to 1 to ensure fair dispatching
                _channel.BasicQos(0, 1, false);
                
                var consumer = new EventingBasicConsumer(_channel);
                
                // Register the basic consumer
                consumer.Received += (sender, args) =>
                {
                    var messageId = Guid.NewGuid().ToString(); // For logging
                    var body = args.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    
                    _logger.LogDebug("Received message {MessageId} from {QueueName}", messageId, queueName);
                    
                    try
                    {
                        // Process the message
                        handler(message);
                        
                        // Acknowledge the message if no exception was thrown
                        _channel.BasicAck(args.DeliveryTag, false);
                        _logger.LogDebug("Message {MessageId} processed successfully", messageId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message {MessageId}", messageId);
                        
                        // Negative acknowledge and requeue the message to try again later
                        _channel.BasicNack(args.DeliveryTag, false, true);
                        _logger.LogDebug("Message {MessageId} requeued for retry", messageId);
                    }
                };
                
                // Start consuming, with explicit ack
                _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
                _logger.LogInformation("Subscribed to {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to queue {QueueName}", queueName);
                throw;
            }
        }
        
        private void OnConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            _logger.LogWarning("RabbitMQ connection shutdown. Reason: {0}", e.ReplyText);
            
            // Try to reconnect when the connection is lost
            Task.Delay(TimeSpan.FromSeconds(5))
                .ContinueWith(t => EnsureConnection());
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            try
            {
                if (_channel?.IsOpen == true)
                {
                    _channel.Close();
                    _channel.Dispose();
                }
                
                if (_connection?.IsOpen == true)
                {
                    _connection.Close();
                    _connection.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during RabbitMQ client disposal");
            }
            
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}