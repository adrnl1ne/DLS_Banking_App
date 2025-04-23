using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client.Events;
using Nest;
using QueryService.DTO;
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
        await _rabbit.open_connection();
        await _rabbit.open_channel();

        var channel = _rabbit.Channel;
        channel.QueueDeclareAsync(queue: "AccountEvents", durable: true, exclusive: false, autoDelete: false);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            Console.WriteLine($"📨 Received message: {json}");

            try
            {
                var doc = JsonSerializer.Deserialize<AccountCreatedEvent>(json);

                if (doc != null)
                {
                    var result = await _elasticClient.IndexDocumentAsync(doc);
                    Console.WriteLine(result.IsValid
                        ? "✅ Indexed to Elasticsearch"
                        : $"❌ Elasticsearch error: {result.DebugInformation}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to process message: {ex.Message}");
            }
        };

        channel.BasicConsumeAsync(queue: "AccountEvents", autoAck: true, consumer: consumer);

        await Task.CompletedTask;
    }
}