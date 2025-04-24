using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TransactionService.Infrastructure.Messaging.Events;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public class RabbitMqClient : IRabbitMqClient, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMqClient> _logger;
        private bool _disposed;

        public RabbitMqClient(IConfiguration configuration, ILogger<RabbitMqClient> logger)
        {
            _logger = logger;
            
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration["RabbitMQ:Host"] ?? "rabbitmq",
                    Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                    UserName = configuration["RabbitMQ:Username"] ?? "guest",
                    Password = configuration["RabbitMQ:Password"] ?? "guest",
                    DispatchConsumersAsync = true
                };
                
                _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}", 
                    factory.HostName, factory.Port);
                
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                
                // Declare exchanges
                _channel.ExchangeDeclare(
                    exchange: "transactions",
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false);
                
                _logger.LogInformation("Successfully connected to RabbitMQ");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }

        public void PublishTransactionCreated(TransactionCreatedEvent @event)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqClient));
            
            try
            {
                var routingKey = "transaction.created";
                PublishMessage(routingKey, @event);
                
                _logger.LogInformation("Published {RoutingKey} event for transaction {TransactionId}", 
                    routingKey, @event.TransferId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish transaction.created event for {TransactionId}", 
                    @event.TransferId);
                throw;
            }
        }

        public void PublishTransactionStatusUpdated(TransactionStatusUpdatedEvent @event)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqClient));
            
            try
            {
                var routingKey = "transaction.status.updated";
                PublishMessage(routingKey, @event);
                
                _logger.LogInformation("Published {RoutingKey} event for transaction {TransactionId}", 
                    routingKey, @event.TransferId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish transaction.status.updated event for {TransactionId}", 
                    @event.TransferId);
                throw;
            }
        }

        // Implement interface methods
        public void PublishMessage<T>(string routingKey, T message)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqClient));
            
            try
            {
                var messageJson = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(messageJson);
                
                _channel.BasicPublish(
                    exchange: "transactions",
                    routingKey: routingKey,
                    basicProperties: null,
                    body: body);
                
                _logger.LogDebug("Message published to {Exchange} with routing key {RoutingKey}", 
                    "transactions", routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing message to routing key {RoutingKey}", routingKey);
                throw;
            }
        }

        public void Publish(string routingKey, string message)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqClient));
            
            try
            {
                var body = Encoding.UTF8.GetBytes(message);
                
                _channel.BasicPublish(
                    exchange: "transactions",
                    routingKey: routingKey,
                    basicProperties: null,
                    body: body);
                
                _logger.LogDebug("String message published to {Exchange} with routing key {RoutingKey}", 
                    "transactions", routingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing string message to routing key {RoutingKey}", routingKey);
                throw;
            }
        }

        public void SubscribeToQueue<T>(string queueName, Action<T> callback)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqClient));
            
            try
            {
                _channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);
                
                var consumer = new EventingBasicConsumer(_channel);
                
                consumer.Received += (sender, eventArgs) =>
                {
                    try
                    {
                        var body = eventArgs.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        var deserialized = JsonSerializer.Deserialize<T>(message);
                        
                        if (deserialized != null)
                        {
                            callback(deserialized);
                        }
                        
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue {QueueName}", queueName);
                        _channel.BasicNack(eventArgs.DeliveryTag, false, true);
                    }
                };
                
                _channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);
                
                _logger.LogInformation("Subscribed to queue {QueueName}", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to queue {QueueName}", queueName);
                throw;
            }
        }

        public async Task ConsumeAsync(string queueName, CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqClient));
            
            try
            {
                _channel.QueueDeclare(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);
                
                var consumer = new AsyncEventingBasicConsumer(_channel);
                
                consumer.Received += async (sender, eventArgs) =>
                {
                    try
                    {
                        var body = eventArgs.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        
                        _logger.LogInformation("Received message from queue {QueueName}: {Message}", 
                            queueName, message);
                        
                        // Process message here
                        await Task.Delay(10, cancellationToken); // Simulate processing
                        
                        _channel.BasicAck(eventArgs.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message from queue {QueueName}", queueName);
                        _channel.BasicNack(eventArgs.DeliveryTag, false, true);
                    }
                };
                
                _channel.BasicConsume(
                    queue: queueName,
                    autoAck: false,
                    consumer: consumer);
                
                _logger.LogInformation("Started consuming from queue {QueueName}", queueName);
                
                // Keep the method running until cancellation is requested
                await Task.Delay(-1, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consuming from queue {QueueName} was cancelled", queueName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming from queue {QueueName}", queueName);
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            _channel?.Close();
            _channel?.Dispose();
            _connection?.Close();
            _connection?.Dispose();
            _disposed = true;
            
            _logger.LogInformation("RabbitMQ connection closed");
        }
    }
}
