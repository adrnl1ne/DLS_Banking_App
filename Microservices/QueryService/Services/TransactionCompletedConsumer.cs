using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nest;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace QueryService.Services
{
    public class TransactionCompletedConsumer : BackgroundService
    {
        private readonly ILogger<TransactionCompletedConsumer> _logger;
        private readonly IElasticClient _elasticClient;
        private readonly RabbitMqConnection _rabbitMqConnection;

        public TransactionCompletedConsumer(
            ILogger<TransactionCompletedConsumer> logger,
            IElasticClient elasticClient,
            RabbitMqConnection rabbitMqConnection)
        {
            _logger = logger;
            _elasticClient = elasticClient;
            _rabbitMqConnection = rabbitMqConnection;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔄 STARTING TransactionCompletedConsumer");

            try
            {
                // Use the existing RabbitMQ connection from QueryService
                await _rabbitMqConnection.OpenConnectionAsync();
                await _rabbitMqConnection.OpenChannelAsync();

                var channel = _rabbitMqConnection.Channel;

                // Declare the TransactionCompleted queue
                await channel.QueueDeclareAsync(
                    queue: "TransactionCompleted",
                    durable: true,
                    exclusive: false,
                    autoDelete: false);

                _logger.LogInformation("✅ Connected to RabbitMQ and declared TransactionCompleted queue");

                // Create consumer using the async API
                var consumer = new AsyncEventingBasicConsumer(channel);
                
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = System.Text.Encoding.UTF8.GetString(body);

                    try
                    {
                        _logger.LogInformation("📨 RECEIVED transaction completed event: {Message}", message);

                        var transactionEvent = JsonSerializer.Deserialize<TransactionCompletedEvent>(message, 
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (transactionEvent != null)
                        {
                            // Index completed transaction in Elasticsearch
                            var response = await _elasticClient.IndexAsync(new
                            {
                                transferId = transactionEvent.TransferId,
                                status = "completed",
                                amount = transactionEvent.Amount,
                                description = transactionEvent.Description,
                                fromAccount = transactionEvent.FromAccount,
                                toAccount = transactionEvent.ToAccount,
                                createdAt = transactionEvent.CreatedAt,
                                completedAt = transactionEvent.CompletedAt,
                                event_type = "TransactionCompleted",
                                timestamp = DateTime.UtcNow
                            }, idx => idx.Index("completed_transactions").Id(transactionEvent.TransferId));

                            if (response.IsValid)
                            {
                                _logger.LogInformation("✅ INDEXED completed transaction {TransferId} in Elasticsearch", transactionEvent.TransferId);
                            }
                            else
                            {
                                _logger.LogError("❌ Failed to index transaction {TransferId}: {Error}",
                                    transactionEvent.TransferId, response.DebugInformation);
                            }
                        }

                        // Acknowledge the message using async API
                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ ERROR processing transaction completed event");
                        // Reject and requeue the message using async API
                        await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                    }
                };

                // Start consuming using async API
                await channel.BasicConsumeAsync(
                    queue: "TransactionCompleted",
                    autoAck: false,
                    consumer: consumer);

                _logger.LogInformation("✅ TransactionCompletedConsumer subscribed successfully");

                // Keep running until cancellation
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 FATAL ERROR in TransactionCompletedConsumer");
                throw;
            }
        }
    }

    public class TransactionCompletedEvent
    {
        public string TransferId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string FromAccount { get; set; } = string.Empty;
        public string ToAccount { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
