using System;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public interface IRabbitMqClient
    {
        bool IsConnected { get; }
        
        void EnsureConnection();
        
        // Add this method to create a raw channel for advanced operations
        IModel CreateChannel();
        
        void DeclareQueue(string queueName, bool durable = true, bool exclusive = false, bool autoDelete = false);
        
        void Publish(string queueName, string message);

        void Subscribe<T>(string queueName, Func<T, Task<bool>> handler) where T : class;
        
    }
}
