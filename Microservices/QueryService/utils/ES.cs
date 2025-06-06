﻿using QueryService.DTO;

namespace QueryService.utils;

public class ES
{
	public static readonly Dictionary<string, Type> indexMap = new()
	{
		{ "users", typeof(UserDocument) },
		{ "transaction", typeof(TransactionDocument) },
		{ "account_created", typeof(AccountCreatedEvent) },
		{ "accounts", typeof(AccountEvent) },
		{ "account_events", typeof(AccountEvent) },
		{ "fraud", typeof(CheckFraudEvent) },
		{ "transaction_history", typeof(TransactionCreatedEvent)},
		{ "deleted_accounts", typeof(DeletedAccount)}
	};
}
