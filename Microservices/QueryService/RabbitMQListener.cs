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

        // Declare the exchange
        await channel.ExchangeDeclareAsync("banking.events", ExchangeType.Topic, durable: true);
        
        // Set up queue bindings for different event types
        var queueBindings = new Dictionary<string, string[]>
        {
            { "AccountEvents", new[] { "AccountCreated", "AccountDeleted", "AccountRenamed", "AccountBalanceUpdated", "BalanceUpdateCompleted", "AccountDeposited" } },
            { "TransactionCreated", new[] { "TransactionCreated" } },
            { "FraudEvents", new[] { "FraudEvents", "CheckFraud" } }
        };

        foreach (var (queueName, routingKeys) in queueBindings)
        {
            // Ensure queues are declared with durable: true to match publisher settings
            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            
            // Bind the queue to multiple routing keys
            foreach (var routingKey in routingKeys)
            {
                await channel.QueueBindAsync(queue: queueName, exchange: "banking.events", routingKey: routingKey);
            }

            var consumer = new AsyncEventingBasicConsumer(channel);
            var capturedQueue = queueName;

            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                // Enhanced debugging
                Console.WriteLine($"📨 Received from [{capturedQueue}]: {json}");
                Console.WriteLine($"Routing Key: {ea.RoutingKey}");

                try
                {
                    switch (capturedQueue)
                    {
                        case "AccountEvents":
                            await HandleAccountEvent(json);
                            break;
                        case "TransactionCreated":
                            await HandleTransactionCreatedEvent(json);
                            break;
                        case "FraudEvents":
                            await HandleFraudEvent(json);
                            break;
                        default:
                            Console.WriteLine($"ℹ️ No handler for queue: {capturedQueue}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error processing message from [{capturedQueue}]: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            };

            await channel.BasicConsumeAsync(queue: capturedQueue, autoAck: true, consumer: consumer);
        }

        await Task.CompletedTask;
    }

    // Handles all account-related events (created, deleted, renamed, balance updated)
    private async Task HandleAccountEvent(string json)
    {
        try {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Check if this is a BalanceUpdateCompleted event (different structure)
            if (root.TryGetProperty("completedAt", out _) && !root.TryGetProperty("event_type", out _))
            {
                Console.WriteLine($"🔍 Processing BalanceUpdateCompleted event");
                // This is just a confirmation event, we don't need to update Elasticsearch
                // The actual balance update was already handled by the AccountBalanceUpdated event
                return;
            }
            
            // Safely get event_type property (should exist for all events except BalanceUpdateCompleted)
            string? eventType = null;
            if (root.TryGetProperty("event_type", out var eventTypeProp))
            {
                eventType = eventTypeProp.GetString();
            }
            
            if (eventType == null)
            {
                Console.WriteLine("⚠️ Event missing event_type property - skipping");
                return;
            }
            
            var accountId = root.GetProperty("accountId").GetInt32();
            
            Console.WriteLine($"🔍 Processing account event: Type={eventType}, AccountId={accountId}");
            
            // Store the event in the "account_events" index (history)
            var accountEvent = JsonSerializer.Deserialize<AccountEvent>(json);
            if (accountEvent != null)
            {
                var historyResponse = await _elasticClient.IndexAsync(accountEvent, idx => idx.Index("account_events"));
                if (historyResponse.IsValid)
                    Console.WriteLine($"✅ Indexed AccountEvent in history: {eventType}");
                else
                    Console.WriteLine($"❌ Failed to index AccountEvent in history: {historyResponse.DebugInformation}");
            }

            // Update the "accounts" index (current state)
            switch (eventType)
            {
                case "AccountCreated":
                    if (accountEvent != null)
                    {
                        Console.WriteLine($"📝 Creating account in ES: AccountId={accountId}, Name={accountEvent.Name}, UserId={accountEvent.UserId}");
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
								.Doc(new AccountEvent
								{
									EventType = accountEvent.EventType,
									Name = accountEvent.Name,
									Timestamp = accountEvent.Timestamp
								})
								.DocAsUpsert(true));
							Console.WriteLine(updateResponse.IsValid
								? $"✅ Account renamed in 'accounts' index."
								: $"❌ Failed to rename account in 'accounts' index: {updateResponse.DebugInformation}");
						}
                    break;

                case "AccountBalanceUpdated":
                case "AccountDeposited":
                    if (accountEvent != null)
						{
							Console.WriteLine($"🔄 Updating account balance: AccountId={accountId}, NewBalance={accountEvent.Amount}");
							
							var updateResponse = await _elasticClient.UpdateAsync<AccountEvent>(accountId, u => u
								.Index("accounts")
								.Doc(new AccountEvent
								{
									EventType = accountEvent.EventType,
									AccountId = accountEvent.AccountId,
									UserId = accountEvent.UserId,
									Name = accountEvent.Name,
									Amount = accountEvent.Amount, // This is the key - update the actual balance
									TransactionId = accountEvent.TransactionId,
									TransactionType = accountEvent.TransactionType,
									Timestamp = accountEvent.Timestamp
								})
								.DocAsUpsert(true));
							Console.WriteLine(updateResponse.IsValid
								? $"✅ Account balance updated in 'accounts' index to {accountEvent.Amount}."
								: $"❌ Failed to update account balance in 'accounts' index: {updateResponse.DebugInformation}");
						}
                    break;

                default:
                    Console.WriteLine($"⚠️ Unknown account event type: {eventType}");
                    break;
            }
        } catch (Exception ex) {
            Console.WriteLine($"❌ Error in HandleAccountEvent: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    // Handles transaction creation events
    private async Task HandleTransactionCreatedEvent(string json)
    {
        try
        {
            var transactionEvent = JsonSerializer.Deserialize<TransactionCreatedEvent>(json);
            if (transactionEvent == null)
            {
                Console.WriteLine("⚠️ Failed to deserialize TransactionCreatedEvent.");
                return;
            }

            Console.WriteLine($"📨 Processing TransactionCreatedEvent: TransferId={transactionEvent.TransferId}, Amount={transactionEvent.Amount}");

            var response = await _elasticClient.IndexAsync(transactionEvent, idx => idx.Index("transaction_history"));
            if (response.IsValid)
                Console.WriteLine($"✅ Indexed TransactionCreatedEvent: TransferId={transactionEvent.TransferId}");
            else
                Console.WriteLine($"❌ Failed to index TransactionCreatedEvent: {response.DebugInformation}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing TransactionCreatedEvent: {ex.Message}");
        }
    }

    // Handles fraud detection results
    private async Task HandleFraudEvent(string json)
    {
        try
        {
            var fraudEvent = JsonSerializer.Deserialize<CheckFraudEvent>(json);
            if (fraudEvent == null)
            {
                Console.WriteLine("⚠️ Failed to deserialize fraud event.");
                return;
            }

            Console.WriteLine($"📨 Processing FraudEvent: TransferId={fraudEvent.TransferId}, IsFraud={fraudEvent.IsFraud}, Status={fraudEvent.Status}");

            var response = await _elasticClient.IndexAsync(fraudEvent, idx => idx.Index("fraud"));
            if (response.IsValid)
                Console.WriteLine($"✅ Indexed FraudEvent: TransferId={fraudEvent.TransferId}");
            else
                Console.WriteLine($"❌ Failed to index FraudEvent: {response.DebugInformation}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error processing FraudEvent: {ex.Message}");
        }
    }
}