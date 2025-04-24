namespace TransactionService.Infrastructure.Messaging.RabbitMQ;

public class RabbitMqConfiguration
{
    public required string HostName { get; init; }
    public int Port { get; init; } = 5672;
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public string VirtualHost { get; init; } = "/";
}