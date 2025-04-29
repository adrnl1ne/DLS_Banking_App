namespace TransactionService.Exceptions;

public class ServiceUnavailableException : Exception
{
    public string ServiceName { get; }

    public ServiceUnavailableException(string serviceName, string message)
        : base(message)
    {
        ServiceName = serviceName;
    }

    public ServiceUnavailableException(string serviceName, string message, Exception innerException)
        : base(message, innerException)
    {
        ServiceName = serviceName;
    }
}