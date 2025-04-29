using System.Text;
using RabbitMQ.Client;

namespace QueryService;

public class RabbitMqConnection : IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private IConnection _connection;
    private IChannel _channel;

    public RabbitMqConnection(string hostName, string userName, string password)
    {
        _factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password
        };
    }

    public IChannel Channel => _channel;

    public async Task<bool> OpenConnectionAsync()
    {
        _connection = await _factory.CreateConnectionAsync();

        if (_connection.IsOpen)
        {
            Console.WriteLine("✅ Connection to RabbitMQ established.");
            return true;
        }

        Console.WriteLine("❌ Failed to connect to RabbitMQ.");
        return false;
    }

    public async Task<bool> OpenChannelAsync()
    {
        _channel = await _connection.CreateChannelAsync();
        if (_channel.IsOpen)
        {
            Console.WriteLine("✅ Channel to RabbitMQ established.");
            return true;
        }

        Console.WriteLine("❌ Failed to open channel to RabbitMQ.");
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
        {
            await _channel.DisposeAsync();
            Console.WriteLine("ℹ️ RabbitMQ channel disposed.");
        }

        if (_connection != null)
        {
            await _connection.DisposeAsync();
            Console.WriteLine("ℹ️ RabbitMQ connection disposed.");
        }
    }
}