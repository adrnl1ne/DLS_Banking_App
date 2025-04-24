using System.Text;
using System.Text.Json;
using RabbitMQ.Client.Events;
using Nest;
using QueryService.DTO;
using RabbitMQ.Client;

namespace QueryService;

public class RabbitMqListener : BackgroundService
{
    private readonly RabbitMqConnection _rabbit;
    private readonly IElasticClient _elasticClient;

    private static readonly Dictionary<string, Type> _queueMap = new()
    {
        { "AccountCreated", typeof(AccountCreatedEvent) },
        { "CheckFraud", typeof(CheckFraudEvent) }
    };

    public RabbitMqListener(RabbitMqConnection rabbit, IElasticClient elasticClient)
    {
        _rabbit = rabbit;
        _elasticClient = elasticClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _rabbit.open_connection();
        await _rabbit.open_channel();

        var channel = _rabbit.Channel;

        foreach (var queue in _queueMap.Keys)
        {
            await channel.QueueDeclareAsync(queue: queue, durable: false, exclusive: false, autoDelete: false);

            
            var consumer = new AsyncEventingBasicConsumer(channel);
            var capturedQueue = queue;

            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                Console.WriteLine($"📨 Received from [{capturedQueue}]: {json}");

                try
                {
                    if (_queueMap.TryGetValue(capturedQueue, out var eventType))
                    {
                        var evt = JsonSerializer.Deserialize(json, eventType);
                        if (evt is not null)
                        {
                            var indexName = capturedQueue.ToLowerInvariant();
                            var response = await _elasticClient.IndexAsync<object>(evt, idx => idx.Index(indexName));
                            Console.WriteLine(response.IsValid
                                ? $"✅ Indexed event from [{capturedQueue}]"
                                : $"❌ Elasticsearch indexing failed: {response.DebugInformation}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ No event type mapped for queue: {capturedQueue}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing message from [{capturedQueue}]: {ex.Message}");
                }
            };

            await channel.BasicConsumeAsync(queue: capturedQueue, autoAck: false, consumer: consumer);
        }

        await Task.CompletedTask;
    }
}
