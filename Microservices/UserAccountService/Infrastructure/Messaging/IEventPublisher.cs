namespace UserAccountService.Infrastructure.Messaging;

public interface IEventPublisher
{
    void Publish(string queueName, string message);
}
