using System;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public interface IRabbitMqClient
    {
        void Publish(string queue, string message);
        void Subscribe(string queue, Action<string> callback);
    }
}
