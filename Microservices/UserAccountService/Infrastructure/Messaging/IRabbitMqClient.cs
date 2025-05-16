using System;
using System.Threading.Tasks;

namespace UserAccountService.Infrastructure.Messaging
{
    public interface IRabbitMqClient : IDisposable
    {
        void Subscribe<T>(string queueName, Func<T, Task<bool>> handler) where T : class;
        void Publish<T>(string queueName, T message) where T : class;
        bool IsConnected { get; }
        void EnsureConnection();
    }
}