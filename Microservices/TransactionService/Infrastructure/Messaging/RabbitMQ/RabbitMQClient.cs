using System;
using System.Text;
using System.Threading.Tasks;
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
                    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest"
                };
                
                _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}", factory.HostName, factory.Port);
                
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                _logger.LogInformation("Successfully connected to RabbitMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        public void Publish(string queue, string message)
        {
            try
            {
                _channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false);

                var body = Encoding.UTF8.GetBytes(message);
                _channel.BasicPublish(exchange: "", routingKey: queue, basicProperties: null, body: body);

                _logger.LogInformation("Published message to {Queue}: {Message}", queue, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to {Queue}", queue);
                throw;
            }
        }

        public void Subscribe(string queue, Action<string> callback)
        {
            try
            {
                _channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false);

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
                throw;
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}
