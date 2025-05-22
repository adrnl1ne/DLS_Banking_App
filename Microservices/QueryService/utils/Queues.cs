using QueryService.DTO;

namespace QueryService.utils;

public class Queues
{
    public static readonly Dictionary<string, Type> queueMap = new()
    {
        { "AccountEvents", typeof(AccountCreatedEvent) },
        { "CheckFraud", typeof(CheckFraudEvent) },
        { "TransactionCreated", typeof(TransactionDocument) },
        { "FraudEvents", typeof(CheckFraudEvent) },
        { "UserCreated", typeof(UserDocument) }
        
    };
}