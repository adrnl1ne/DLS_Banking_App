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
                        if (evt is null)
                        {
                            Console.WriteLine($"⚠️ Could not deserialize event for queue: {capturedQueue}");
                            return;
                        }

                        switch (capturedQueue)
                        {
                            case "AccountEvents":
                                await HandleAccountEvent(json);
                                break;
                            
                            case "FraudEvents":
                                await HandleFraudEvent(json);
                                break;
                            
                            case "TransactionCreated":
                                await HandleTransactionCreatedEvent(json);
                                break;

                            default:
                                Console.WriteLine($"ℹ️ No specific handler for queue: {capturedQueue}");
                                break;
                        }

                        // Common indexing logic (if needed for all events)
                        var indexName = QueueIndexMapper.AccountEvents(capturedQueue.ToLowerInvariant());
                        var response = await _elasticClient.IndexAsync<object>(evt, idx => idx.Index(indexName));
                        Console.WriteLine(response.IsValid
                            ? $"✅ Indexed event from [{capturedQueue}]"
                            : $"❌ Elasticsearch indexing failed: {response.DebugInformation}");
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

    private async Task HandleAccountEvent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var eventType = root.GetProperty("event_type").GetString();
        var accountId = root.GetProperty("accountId").GetInt32();

        // Store the event in the "account_events" index (history)
        var accountEvent = JsonSerializer.Deserialize<AccountEvent>(json);
        if (accountEvent != null)
        {
            var historyResponse = await _elasticClient.IndexAsync(accountEvent, idx => idx.Index("account_events"));
            if (historyResponse.IsValid)
            {
                Console.WriteLine($"✅ Successfully indexed AccountEvent in history: {eventType}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to index AccountEvent in history: {historyResponse.DebugInformation}");
            }
        }

        // Update the "accounts" index (current state)
        switch (eventType)
        {
            case "AccountCreated":
                if (accountEvent != null)
                {
                    var createResponse = await _elasticClient.IndexAsync(accountEvent, idx => idx.Index("accounts").Id(accountId));
                    Console.WriteLine(createResponse.IsValid
                        ? $"✅ Account created in 'accounts' index."
                        : $"❌ Failed to create account in 'accounts' index: {createResponse.DebugInformation}");
                }
                break;

            case "AccountDeleted":
                var deleteResponse = await _elasticClient.DeleteAsync<AccountEvent>(accountId, d => d.Index("accounts"));
                Console.WriteLine(deleteResponse.IsValid
                    ? $"✅ Account deleted from 'accounts' index."
                    : $"❌ Failed to delete account from 'accounts' index: {deleteResponse.DebugInformation}");
                break;

            case "AccountRenamed":
                if (accountEvent != null)
                {
                    var updateResponse = await _elasticClient.UpdateAsync<AccountEvent>(accountId, u => u
                        .Index("accounts")
                        .Doc(new AccountEvent { Name = accountEvent.Name }));
                    Console.WriteLine(updateResponse.IsValid
                        ? $"✅ Account renamed in 'accounts' index."
                        : $"❌ Failed to rename account in 'accounts' index: {updateResponse.DebugInformation}");
                }
                break;

            case "AccountBalanceUpdated":
                if (accountEvent != null)
                {
                    var updateResponse = await _elasticClient.UpdateAsync<AccountEvent>(accountId, u => u
                        .Index("accounts")
                        .Doc(new AccountEvent { Amount = accountEvent.Amount }));
                    Console.WriteLine(updateResponse.IsValid
                        ? $"✅ Account balance updated in 'accounts' index."
                        : $"❌ Failed to update account balance in 'accounts' index: {updateResponse.DebugInformation}");
                }
                break;
            
            case "AccountDeposited":
                if (accountEvent != null)
                {
                    var updateResponse = await _elasticClient.UpdateAsync<AccountEvent>(accountId, u => u
                        .Index("accounts")
                        .Doc(new AccountEvent { Amount = accountEvent.Amount }));
                    Console.WriteLine(updateResponse.IsValid
                        ? $"✅ Account balance updated in 'accounts' index after deposit."
                        : $"❌ Failed to update account balance in 'accounts' index: {updateResponse.DebugInformation}");
                }
                break;

            default:
                Console.WriteLine($"⚠️ Unknown event type: {eventType}");
                break;
        }
    }
    
    private async Task HandleFraudEvent(string json)
    {
        try
        {
            // Parse the incoming message
            var fraudEvent = JsonSerializer.Deserialize<CheckFraudEvent>(json);
            if (fraudEvent == null)
            {
                Console.WriteLine("⚠️ Failed to deserialize fraud event.");
                return;
            }

            // Log the event
            Console.WriteLine($"📨 Processing FraudEvent: TransferId={fraudEvent.TransferId}, IsFraud={fraudEvent.IsFraud}, Status={fraudEvent.Status}");

            // Index the fraud event in Elasticsearch
            var response = await _elasticClient.IndexAsync(fraudEvent, idx => idx.Index("fraud"));
            if (response.IsValid)
            {
                Console.WriteLine($"✅ Successfully indexed FraudEvent: TransferId={fraudEvent.TransferId}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to index FraudEvent: {response.DebugInformation}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing FraudEvent: {ex.Message}");
        }
    }
    
    private async Task HandleTransactionCreatedEvent(string json)
    {
        try
        {
            // Deserialize the event
            var transactionEvent = JsonSerializer.Deserialize<TransactionCreatedEvent>(json);
            if (transactionEvent == null)
            {
                Console.WriteLine("⚠️ Failed to deserialize TransactionCreatedEvent.");
                return;
            }

            // Log the event
            Console.WriteLine($"📨 Processing TransactionCreatedEvent: TransferId={transactionEvent.TransferId}, Amount={transactionEvent.Amount}");

            // Index the event in Elasticsearch
            var response = await _elasticClient.IndexAsync(transactionEvent, idx => idx.Index("transaction_history"));
            if (response.IsValid)
            {
                Console.WriteLine($"✅ Successfully indexed TransactionCreatedEvent: TransferId={transactionEvent.TransferId}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to index TransactionCreatedEvent: {response.DebugInformation}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing TransactionCreatedEvent: {ex.Message}");
        }
    }
    

    public class QueueIndexMapper
    {
        public static string AccountEvents(string queue)
        {
            // You can map different queues to different index names if needed
            return queue switch
            {
                "account-created" => "account_created",
                "transaction-initiated" => "transaction_initiated",
                "account-events" => "account_events",
                _ => queue
            };
        }
    }
}