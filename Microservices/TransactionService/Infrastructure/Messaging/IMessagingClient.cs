namespace TransactionService.Infrastructure.Messaging;

/// <summary>
/// Interface for messaging clients to abstract away specific implementations (RabbitMQ, Azure Service Bus, etc.)
/// </summary>
public interface IMessagingClient
{
    /// <summary>
    /// Publishes a message to a specified queue
    /// </summary>
    /// <param name="queue">The name of the queue</param>
    /// <param name="message">The message content as JSON string</param>
    void Publish(string queue, string message);
    
    /// <summary>
    /// Subscribes to a queue and processes received messages
    /// </summary>
    /// <param name="queue">The name of the queue</param>
    /// <param name="callback">The callback to execute when a message is received</param>
    void Subscribe(string queue, Action<string> callback);
}