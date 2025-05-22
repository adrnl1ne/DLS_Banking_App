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

    // Handles all account-related events (created, deleted, renamed, balance updated)
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
                    var createResponse = await _elasticClient.IndexAsync(accountEvent, idx => idx.Index("accounts").Id(accountId));
                    Console.WriteLine(createResponse.IsValid
                        ? $"✅ Account created in 'accounts' index."
                        : $"❌ Failed to create account in 'accounts' index: {createResponse.DebugInformation}");
                }
                break;

            case "AccountDeleted":
                if (accountEvent != null)
				{
					// Store tombstone in "deleted_accounts" index
					var deletedAccount = new DeletedAccount
					{
						AccountId = accountEvent.AccountId,
						UserId = accountEvent.UserId,
						Name = accountEvent.Name,
						Timestamp = accountEvent.Timestamp
					};

					var tombstoneResponse = await _elasticClient.IndexAsync(deletedAccount, idx => idx.Index("deleted_accounts"));
					if (tombstoneResponse.IsValid)
						Console.WriteLine($"✅ Tombstone written to 'deleted_accounts' for AccountId={accountEvent.AccountId}");
					else
						Console.WriteLine($"❌ Failed to write tombstone: {tombstoneResponse.DebugInformation}");

					// Delete the actual account from "accounts" index
					var deleteResponse = await _elasticClient.DeleteAsync<AccountEvent>(accountEvent.AccountId, d => d.Index("accounts"));
					Console.WriteLine(deleteResponse.IsValid
						? $"✅ Account deleted from 'accounts' index."
						: $"❌ Failed to delete account from 'accounts' index: {deleteResponse.DebugInformation}");
				}
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
						// Fetch the existing document
						var getResponse = await _elasticClient.GetAsync<AccountEvent>(accountId, g => g.Index("accounts"));
						var existing = getResponse.Source;

						// If the document exists, update only the relevant fields
						if (existing != null)
						{
							existing.Amount = accountEvent.Amount;
							existing.Timestamp = accountEvent.Timestamp;
							existing.EventType = accountEvent.EventType;

							var updateResponse = await _elasticClient.IndexAsync(existing, idx => idx.Index("accounts").Id(accountId));
							// handle response...
						}
						else
						{
							// If not found, create a new document with all required fields
							var newDoc = new AccountEvent
							{
								AccountId = accountEvent.AccountId,
								UserId = accountEvent.UserId,
								Name = accountEvent.Name,
								Amount = accountEvent.Amount,
								Timestamp = accountEvent.Timestamp,
								EventType = accountEvent.EventType
								// ...set other required fields as needed
							};
							var createResponse = await _elasticClient.IndexAsync(newDoc, idx => idx.Index("accounts").Id(accountId));
							// handle response...
						}
					}
                break;

            default:
                Console.WriteLine($"⚠️ Unknown account event type: {eventType}");
                break;
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