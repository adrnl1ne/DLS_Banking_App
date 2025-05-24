namespace TransactionService.Infrastructure.Messaging.RabbitMQ
{
    public interface IRabbitMQConfiguration
    {
        string Host { get; }
        int Port { get; }
        string Username { get; }
        string Password { get; }
    }
}