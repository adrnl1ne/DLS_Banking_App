using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace UserAccountService.Infrastructure.Messaging
{
    public class AsyncEventingConsumer<T> : AsyncEventingBasicConsumer where T : class
    {
        private readonly ILogger _logger;
        private readonly Func<T, Task<bool>> _handler;
        private readonly string _queueName;
        private readonly IModel _channel;
        private readonly JsonSerializerOptions _jsonOptions;

        public AsyncEventingConsumer(
            IModel channel,
            ILogger logger,
            string queueName,
            Func<T, Task<bool>> handler) : base(channel)
        {
            _channel = channel;
            _logger = logger;
            _queueName = queueName;
            _handler = handler;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public override async Task HandleBasicDeliver(
            string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties properties,
            ReadOnlyMemory<byte> body)
        {
            string message = Encoding.UTF8.GetString(body.ToArray());
            _logger.LogInformation("Message received from queue {QueueName}, Length={Length}bytes", 
                _queueName, body.Length);

            try
            {
                T? typedMessage = null;
                try
                {
                    typedMessage = JsonSerializer.Deserialize<T>(message, _jsonOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize message: {Message}", message);
                    _channel.BasicNack(deliveryTag, false, false); // Don't requeue bad messages
                    return;
                }

                if (typedMessage == null)
                {
                    _logger.LogWarning("Received null message after deserialization");
                    _channel.BasicNack(deliveryTag, false, false); // Don't requeue null messages
                    return;
                }

                bool success;
                try
                {
                    success = await _handler(typedMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    _channel.BasicNack(deliveryTag, false, true); // Requeue on error
                    return;
                }

                if (success)
                {
                    _logger.LogInformation("Successfully processed message, sending ACK");
                    _channel.BasicAck(deliveryTag, false);
                }
                else
                {
                    _logger.LogWarning("Handler declined to process message, requeueing");
                    _channel.BasicNack(deliveryTag, false, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in message consumer");
                try
                {
                    _channel.BasicNack(deliveryTag, false, true);
                }
                catch (Exception nackEx)
                {
                    _logger.LogError(nackEx, "Failed to NACK message");
                }
            }
        }
    }
}
