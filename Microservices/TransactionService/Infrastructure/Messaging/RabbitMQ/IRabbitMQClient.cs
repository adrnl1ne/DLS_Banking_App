using System;
using System.Threading;
using System.Threading.Tasks;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ;

public interface IRabbitMQClient
{
    /// <summary>
    /// Publishes a typed message to the specified queue after serializing it to JSON
    /// </summary>
    void PublishMessage<T>(string queue, T message);
    
    /// <summary>
    /// Publishes a string message to the specified queue
    /// </summary>
    void Publish(string queueName, string message);
    
    /// <summary>
    /// Subscribes to a queue and invokes the handler for each message
    /// </summary>
    void SubscribeToQueue<T>(string queue, Action<T> handler);
    
    /// <summary>
    /// Consumes a single message from the specified queue asynchronously
    /// </summary>
    Task<string> ConsumeAsync(string queueName, CancellationToken cancellationToken = default);
}
