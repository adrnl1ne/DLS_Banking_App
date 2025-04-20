namespace TransactionService.API.Infrastructure.Messaging.RabbitMQ;

public class RabbitMQConfiguration
{
    public required string HostName { get; set; }
    public int Port { get; set; } = 5672;
    public required string UserName { get; set; }
    public required string Password { get; set; }
    public string VirtualHost { get; set; } = "/";
}