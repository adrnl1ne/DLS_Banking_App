namespace TransactionService.Infrastructure.Messaging.RabbitMQ;

public abstract class RabbitMqConfiguration
{
    public required string HostName { get; set; }
    public int Port { get; set; } = 5672;
    public required string UserName { get; set; }
    public required string Password { get; set; }
    public string VirtualHost { get; set; } = "/";
}