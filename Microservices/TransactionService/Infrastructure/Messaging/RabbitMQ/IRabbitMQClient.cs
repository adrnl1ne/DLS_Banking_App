using System;
using System.Threading;
using System.Threading.Tasks;
using TransactionService.Infrastructure.Messaging.Events;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public interface IRabbitMqClient
    {
        void PublishTransactionCreated(TransactionCreatedEvent @event);
        void PublishTransactionStatusUpdated(TransactionStatusUpdatedEvent @event);
        void PublishMessage<T>(string routingKey, T message);
        void Publish(string routingKey, string message);
        void SubscribeToQueue<T>(string queueName, Action<T> callback);
        Task ConsumeAsync(string queueName, CancellationToken cancellationToken);
        void Dispose();
    }
}
