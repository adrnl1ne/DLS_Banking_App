using Nest;

namespace QueryService;

public class Query
{
    public async Task<List<TransactionEvent>> GetTransactions(
        [Service] IElasticClient elasticClient,
        string username,
        bool? isFraud = null)
    {
        var search = await elasticClient.SearchAsync<TransactionEvent>(s => s
            .Query(q => q
                .Bool(b => b
                    .Must(
                        q.Match(m => m.Field(f => f.Username).Query(username)),
                        isFraud.HasValue 
                            ? q.Term(t => t.Field(f => f.IsFraud).Value(isFraud.Value)) 
                            : null
                    )
                )
            )
        );

        return search.Documents.ToList();
    }
}
