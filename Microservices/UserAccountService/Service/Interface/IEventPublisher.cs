namespace AccountService.Services;

public interface IEventPublisher
{
    void Publish(string queueName, string message);
}
