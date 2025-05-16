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
        private IConnection _connection;
        private IModel _channel;
        private bool _disposed;
        
        public bool IsConnected => _connection?.IsOpen == true && !_disposed;

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
                
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _hostName,
                    Port = _port,
                    UserName = _username,
                    Password = _password,
                    RequestedHeartbeat = TimeSpan.FromSeconds(30),
                    DispatchConsumersAsync = true
                };
                
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                // Declare queues that we'll use
                _channel.QueueDeclare("AccountBalanceUpdates", durable: true, exclusive: false, autoDelete: false);
                
                _connection.ConnectionShutdown += OnConnectionShutdown;
                _logger.LogInformation("RabbitMQ connection established to {Host}:{Port}", _hostName, _port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not connect to RabbitMQ");
            }
        }

        public void Publish<T>(string queueName, T message) where T : class
        {
            EnsureConnection();
            
            try
            {
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);
                
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;
                
                _channel.BasicPublish(
                    exchange: "",
                    routingKey: queueName,
                    basicProperties: properties,
                    body: body);
                
                _logger.LogInformation("Message published to {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing message to {QueueName}", queueName);
                throw;
            }
        }
        
        public void Subscribe<T>(string queueName, Func<T, Task<bool>> handler) where T : class
        {
            EnsureConnection();
            
            var consumer = new AsyncEventingBasicConsumer(_channel);
            
            consumer.Received += async (sender, args) =>
            {
                try
                {
                    var message = Encoding.UTF8.GetString(args.Body.ToArray());
                    _logger.LogInformation("Received message from {QueueName}", queueName);
                    
                    var obj = JsonSerializer.Deserialize<T>(message);
                    if (obj == null)
                    {
                        _logger.LogWarning("Failed to deserialize message from {QueueName}", queueName);
                        _channel.BasicNack(args.DeliveryTag, false, false);
                        return;
                    }
                    
                    bool success = false;
                    try
                    {
                        success = await handler(obj);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from {QueueName}", queueName);
                        // Requeue the message if it's a processing error
                        _channel.BasicNack(args.DeliveryTag, false, true);
                        return;
                    }
                    
                    if (success)
                    {
                        _channel.BasicAck(args.DeliveryTag, false);
                    }
                    else
                    {
                        // If handler returns false, requeue the message
                        _channel.BasicNack(args.DeliveryTag, false, true);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize message from {QueueName}", queueName);
                    // Don't requeue messages that can't be deserialized
                    _channel.BasicNack(args.DeliveryTag, false, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing message from {QueueName}", queueName);
                    // Requeue on unexpected errors
                    _channel.BasicNack(args.DeliveryTag, false, true);
                }
            };
            
            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            _logger.LogInformation("Subscribed to {QueueName}", queueName);
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
            
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}