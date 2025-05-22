using Nest;
using QueryService.DTO;

namespace QueryService;

public class Query
{
    public async Task<List<AccountEvent>> GetAccounts(
        [Service] IElasticClient elasticClient,
        int? userId = null)
    {
        var search = await elasticClient.SearchAsync<AccountEvent>(s => s
            .Index("accounts")
            .Query(q => userId.HasValue
                ? q.Term(t => t.UserId, userId.Value)
                : q.MatchAll()
            )
        );
        return search.Documents.ToList();
    }
	
	public async Task<List<AccountEvent>> GetAccountHistory(
        [Service] IElasticClient elasticClient,
        int accountId)
    {
        var search = await elasticClient.SearchAsync<AccountEvent>(s => s
            .Index("account_events")
            .Query(q => q.Term(t => t.AccountId, accountId))
            .Sort(srt => srt.Ascending(f => f.Timestamp))
        );
        return search.Documents.ToList();
    }

    public async Task<List<TransactionCreatedEvent>> GetTransactions(
		[Service] IElasticClient elasticClient,
		string? accountId = null)
	{
		var search = await elasticClient.SearchAsync<TransactionCreatedEvent>(s => s
			.Index("transaction_history")
			.Query(q => !string.IsNullOrEmpty(accountId)
				? q.Term(t => t.FromAccount, accountId)
				: q.MatchAll()
			)
		);
		return search.Documents.ToList();
	}

    public async Task<List<CheckFraudEvent>> GetFraudEvents(
		[Service] IElasticClient elasticClient,
		string? transferId = null)
	{
		try
		{
			var search = await elasticClient.SearchAsync<CheckFraudEvent>(s => s
				.Index("fraud")
				.Query(q => !string.IsNullOrEmpty(transferId)
					? q.Term(t => t.TransferId, transferId)
					: q.MatchAll()
				)
			);

			Console.WriteLine($"Found {search.Documents.Count} fraud events.");
			return search.Documents.ToList();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error in GetFraudEvents: {ex.Message}");
			return new List<CheckFraudEvent>();
		}
	}
}