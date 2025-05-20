using QueryService.DTO;

namespace QueryService.utils;

public class ES
{
	public static readonly Dictionary<string, Type> indexMap = new()
	{
		{ "users", typeof(UserDocument) },
		{ "transaction", typeof(TransactionDocument) },
		{ "account_created", typeof(AccountCreatedEvent) },
		{ "fraud", typeof(CheckFraudEvent) },
		{ "transaction_history", typeof(TransactionCreatedEvent)}
	};
}
