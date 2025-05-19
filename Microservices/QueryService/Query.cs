using Nest;
using QueryService.DTO;

namespace QueryService;

public class Query
{
    public async Task<List<AccountCreatedEvent>> GetAccounts(
        [Service] IElasticClient elasticClient,
        int? userId = null)
    {
        var search = await elasticClient.SearchAsync<AccountCreatedEvent>(s => s
            .Index("account_created")
            .Query(q => userId.HasValue
                ? q.Term(t => t.UserId, userId.Value)
                : q.MatchAll()
            )
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
        var search = await elasticClient.SearchAsync<CheckFraudEvent>(s => s
            .Index("fraud")
            .Query(q => !string.IsNullOrEmpty(transferId)
                ? q.Term(t => t.TransferId, transferId)
                : q.MatchAll()
            )
        );
        return search.Documents.ToList();
    }
}