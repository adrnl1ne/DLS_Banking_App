using System;
using System.Threading.Tasks;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public interface IRabbitMQClient
    {
        bool IsConnected { get; }
        void EnsureConnection();
        void Publish(string queue, string message);
        void Subscribe(string queue, Action<string> callback);
        void Subscribe<T>(string queue, Func<T, Task<bool>> callback) where T : class;
    }
}
