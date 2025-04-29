using System.Text;
using System.Text.Json;
using RabbitMQ.Client.Events;
using Nest;
using QueryService.DTO;
using QueryService.utils;
using RabbitMQ.Client;

namespace QueryService;

public class RabbitMqListener : BackgroundService
{
    private readonly RabbitMqConnection _rabbit;
    private readonly IElasticClient _elasticClient;

    

    public RabbitMqListener(RabbitMqConnection rabbit, IElasticClient elasticClient)
    {
        _rabbit = rabbit;
        _elasticClient = elasticClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _rabbit.OpenConnectionAsync();
        await _rabbit.OpenChannelAsync();

        var channel = _rabbit.Channel;

        foreach (var queue in Queues.queueMap.Keys)
        {
            await channel.ExchangeDeclareAsync("banking.events", ExchangeType.Topic, durable: true);
            await channel.QueueDeclareAsync(queue: queue, durable: false, exclusive: false, autoDelete: false);
            await channel.QueueBindAsync(queue: queue, exchange: "banking.events", routingKey: queue);
            
            var consumer = new AsyncEventingBasicConsumer(channel);
            var capturedQueue = queue;

            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                Console.WriteLine($"📨 Received from [{capturedQueue}]: {json}");
                

                
                try
                {
                    if (Queues.queueMap.TryGetValue(capturedQueue, out var eventType))
                    {
                        var evt = JsonSerializer.Deserialize(json, eventType);
                        if (evt is AccountCreatedEvent accountCreated)
                        {
                            Console.WriteLine(
                                $"Parsed event → AccountId: {accountCreated.AccountId}, UserId: {accountCreated.UserId}");
                        }

                        if (evt is not null)
                        {
                            var indexName = QueueIndexMapper.AccountEvents(capturedQueue.ToLowerInvariant());
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

    public class QueueIndexMapper
    {
        public static string AccountEvents(string queue)
        {
            return "account_created";
        }
    }
}
