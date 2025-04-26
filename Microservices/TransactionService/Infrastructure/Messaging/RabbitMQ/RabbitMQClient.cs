using System;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public class RabbitMqClient : IRabbitMqClient, IDisposable
    {
        private readonly ILogger<RabbitMqClient> _logger;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private bool _initialized = false;

        public RabbitMqClient(ILogger<RabbitMqClient> logger)
        {
            _logger = logger;
            
            try
            {
                var factory = new ConnectionFactory
                { 
                    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq",
                    Port = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672"),
                    UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest",
                    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest",
                    // Add retry logic for connection
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
                };
                
                _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}", factory.HostName, factory.Port);
                
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                _logger.LogInformation("Successfully connected to RabbitMQ");
                _initialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ. Will operate in fallback mode.");
                // Don't throw - we'll run in a degraded mode
            }
        }

        public void Publish(string queue, string message)
        {
            if (!_initialized)
            {
                _logger.LogWarning("RabbitMQ client not initialized. Message to {Queue} not sent: {Message}", queue, message);
                return;
            }

            try
            {
                // First try to declare queue as non-durable to match existing configuration
                try
                {
                    _channel.QueueDeclare(queue: queue, 
                                         durable: false, 
                                         exclusive: false, 
                                         autoDelete: false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not declare queue {Queue} as non-durable. Will try passive declaration.", queue);
                    // Try passive declaration to use existing queue with its current settings
                    _channel.QueueDeclarePassive(queue);
                }

                var body = Encoding.UTF8.GetBytes(message);
                var properties = _channel.CreateBasicProperties();
                properties.Persistent = true; // Make message persistent even if queue isn't durable
                
                _channel.BasicPublish(exchange: "", 
                                      routingKey: queue, 
                                      basicProperties: properties, 
                                      body: body);

                _logger.LogInformation("Published message to {Queue}: {Message}", queue, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to {Queue}", queue);
                // Don't throw, allow the application to continue
            }
        }

        public void Subscribe(string queue, Action<string> callback)
        {
            if (!_initialized)
            {
                _logger.LogWarning("RabbitMQ client not initialized. Cannot subscribe to {Queue}", queue);
                return;
            }

            try
            {
                // First try passive declaration to use existing queue with its current settings
                try
                {
                    _channel.QueueDeclarePassive(queue);
                    _logger.LogInformation("Found existing queue: {Queue}", queue);
                }
                catch
                {
                    // If the queue doesn't exist, create it as non-durable
                    _channel.QueueDeclare(queue: queue, 
                                         durable: false, 
                                         exclusive: false, 
                                         autoDelete: false);
                    _logger.LogInformation("Created new queue: {Queue}", queue);
                }

                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    
                    _logger.LogInformation("Received message from {Queue}: {Message}", queue, message);

                    try
                    {
                        callback(message);
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from {Queue}", queue);
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                };

                _channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
                _logger.LogInformation("Subscribed to {Queue}", queue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to subscribe to {Queue}", queue);
                // Don't throw, operate in degraded mode
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}
