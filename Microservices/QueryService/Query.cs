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
		try
		{
			Console.WriteLine($"🔍 Querying account history for AccountId={accountId}");
			
			var search = await elasticClient.SearchAsync<AccountEvent>(s => s
				.Index("account_events")
				.Size(100) // Ensure we get enough results
				.Query(q => q
					.Bool(b => b
						.Must(m => m.Term(t => t.Field("accountId").Value(accountId)))
					)
				)
				// Remove sorting temporarily to fix the error
			);

			Console.WriteLine($"📊 Found {search.Documents.Count} history records for AccountId={accountId}");
			Console.WriteLine($"📝 Debug info: IsValid={search.IsValid}, Took={search.Took}ms");
			
			if (!search.IsValid)
			{
				Console.WriteLine($"❌ Search error: {search.DebugInformation}");
			}
			else if (search.Documents.Count == 0)
			{
				// Check if index exists and has documents
				var countResponse = await elasticClient.CountAsync<AccountEvent>(c => c.Index("account_events"));
				Console.WriteLine($"💡 Total documents in account_events index: {countResponse.Count}");
				
				// Get a sample of documents to check structure
				var sampleSearch = await elasticClient.SearchAsync<AccountEvent>(s => s
					.Index("account_events")
					.Size(5)
				);
				
				if (sampleSearch.Documents.Any())
				{
					Console.WriteLine("💡 Sample document from index:");
					var sample = sampleSearch.Documents.First();
					Console.WriteLine($"    EventType: {sample.EventType}, AccountId: {sample.AccountId}");
				}
			}
			
			return search.Documents.ToList();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"❌ Error in GetAccountHistory: {ex.Message}");
			return new List<AccountEvent>();
		}
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
	
	public async Task<List<DeletedAccount>> GetDeletedAccounts(
        [Service] IElasticClient elasticClient,
        int? userId = null)
    {
        var search = await elasticClient.SearchAsync<DeletedAccount>(s => s
            .Index("deleted_accounts")
            .Query(q => userId.HasValue
                ? q.Term(t => t.UserId, userId.Value)
                : q.MatchAll()
            )
            .Sort(srt => srt.Descending(f => f.Timestamp))
        );
        return search.Documents.ToList();
    }
}