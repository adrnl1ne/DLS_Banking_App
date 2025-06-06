using System;
using System.Threading.Tasks;

namespace UserAccountService.Infrastructure.Messaging
{
    public interface IRabbitMqClient : IDisposable
    {
        void Subscribe<T>(string queueName, Func<T, Task<bool>> handler) where T : class;
        void SubscribeAsync<T>(string queueName, Func<T, Task<bool>> handler) where T : class;
        void Publish<T>(string queueName, T message) where T : class;
        void PublishToQueue(string queueName, string message); 
        void PublishToExchange(string exchange, string routingKey, string message);
        bool IsConnected { get; }
        void EnsureConnection();
        void EnsureQueueExists(string queueName, bool durable);
    }
}