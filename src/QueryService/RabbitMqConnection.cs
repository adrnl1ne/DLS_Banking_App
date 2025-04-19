using RabbitMQ.Client;

namespace QueryService;

public class RabbitMqConnection
{

    private ConnectionFactory _factory;
    private IConnection _connection;
    private IChannel _channel;
    
    public RabbitMqConnection(string hostName, string userName, string password)
    {
        _factory = new ConnectionFactory()
        {
            HostName = hostName,
            UserName = userName,
            Password = password
        };
    }
    
    public IChannel Channel => _channel;
    
    public async Task<bool> open_connection()
    {
        _connection = await _factory.CreateConnectionAsync();
        
        if (_connection.IsOpen)
        {
            Console.WriteLine("Connection to RabbitMQ established.");
            return true;
        }
        Console.WriteLine("Failed to connect to RabbitMQ.");
        return false;
    }
    
    public async Task<bool> close_connection()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
        
        if (_connection.IsOpen)
        {
            Console.WriteLine("Failed to close connection to RabbitMQ.");
            return false;
        }
        Console.WriteLine("Connection to RabbitMQ closed.");
        return true;
    }

    public async Task<bool> open_channel()
    {
        _channel = await _connection.CreateChannelAsync();
        if (_channel.IsOpen)
        {
            Console.WriteLine("Channel to RabbitMQ established.");
            return true;
        }
        
        Console.WriteLine("Failed to open channel to RabbitMQ.");
        return false;
    }
}