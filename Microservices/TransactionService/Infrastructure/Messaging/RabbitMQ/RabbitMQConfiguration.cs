using Microsoft.Extensions.Configuration;

namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public class RabbitMQConfiguration : IRabbitMQConfiguration
    {
        public string Host { get; }
        public int Port { get; }
        public string Username { get; }
        public string Password { get; }

        public RabbitMQConfiguration(IConfiguration configuration)
        {
            Host = configuration["RabbitMQ:Host"] ?? "rabbitmq";
            Port = int.TryParse(configuration["RabbitMQ:Port"], out var port) ? port : 5672;
            Username = configuration["RabbitMQ:Username"] ?? "guest";
            Password = configuration["RabbitMQ:Password"] ?? "guest";
        }
    }
}