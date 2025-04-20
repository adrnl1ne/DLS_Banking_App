namespace TransactionService.API.Infrastructure.Messaging.RabbitMQ;

public interface IRabbitMQClient
{
    void PublishMessage<T>(string queue, T message);
    void SubscribeToQueue<T>(string queue, Action<T> handler);
}