using System;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public class RabbitMqClient : IRabbitMqClient, IDisposable
    {
        private readonly string _hostName;
        private readonly int _port;
        private readonly string _userName;
        private readonly string _password;
        private IConnection _connection;
        private IModel _channel;
        private readonly ILogger<RabbitMqClient> _logger;
        private bool _disposed;

        public RabbitMqClient(
            ILogger<RabbitMqClient> logger,
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
            
            Connect();
        }

        private void Connect()
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _hostName,
                    Port = _port,
                    UserName = _userName,
                    Password = _password,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                _logger.LogInformation("Connected to RabbitMQ");
                
                // Don't declare queues at startup - just use them as they are
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        public void EnsureConnection()
        {
            if (_connection?.IsOpen != true || _channel?.IsOpen != true)
            {
                _logger.LogInformation("Reconnecting to RabbitMQ...");
                Dispose();
                Connect();
            }
        }
        
        // Use passive declaration to check if queue exists with any configuration
        private bool QueueExists(string queueName)
        {
            try
            {
                _channel.QueueDeclarePassive(queueName);
                return true;
            }
            catch
            {
                // Queue doesn't exist
                return false;
            }
        }
        
        // Only creates queues that don't exist yet
        private void EnsureQueueExists(string queueName)
        {
            try
            {
                if (!QueueExists(queueName))
                {
                    _logger.LogInformation("Queue {QueueName} doesn't exist, creating as non-durable", queueName);
                    _channel.QueueDeclare(
                        queue: queueName,
                        durable: false, // Set to false to match existing queue settings
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring queue {QueueName} exists", queueName);
                // Don't rethrow - we'll try again later
            }
        }
        
        public void Publish(string queueName, string message)
        {
            try
            {
                EnsureConnection();
                EnsureQueueExists(queueName);

                var body = Encoding.UTF8.GetBytes(message);
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true;  // Make messages persistent
                properties.DeliveryMode = 2;   // Make messages persistent (2 = persistent)

                _channel.BasicPublish(
                    exchange: "", 
                    routingKey: queueName, 
                    basicProperties: properties, 
                    body: body);
                
                _logger.LogInformation("Published message to queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing message to {QueueName}", queueName);
                throw;
            }
        }
        
        public void Subscribe(string queueName, Action<string> handler)
        {
            try
            {
                EnsureConnection();
                EnsureQueueExists(queueName);
                
                _channel.BasicQos(0, 1, false);
                
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        
                        handler(message);
                        
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message");
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };
                
                _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
                _logger.LogInformation("Subscribed to queue: {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to queue {QueueName}", queueName);
                throw;
            }
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
                _logger.LogError(ex, "Error closing RabbitMQ connections");
            }
            
            _disposed = true;
        }
    }
}
