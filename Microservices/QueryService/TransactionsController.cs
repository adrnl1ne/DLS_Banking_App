using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Nest;
using QueryService.DTO;
using QueryService.utils;

namespace QueryService;

[ApiController]
[Route("api/transaction/")]
public class TransactionsController : ControllerBase
{
    
    private readonly IElasticClient elasticsearchClient;
    
    public TransactionsController(IElasticClient elasticsearchClient)
    {
        this.elasticsearchClient = elasticsearchClient;
    }
    
    [HttpGet("accounts/{accountId}/transactions")]
    public async Task<IActionResult> GetTransactionHistory(string accountId, [FromQuery] string fromDate = null, [FromQuery] string toDate = null)
    {
        var filters = new List<QueryContainer>();

        filters.Add(new TermQuery { Field = "fromAccount.keyword", Value = accountId });

        if (!string.IsNullOrEmpty(fromDate))
        {
            filters.Add(new DateRangeQuery
            {
                Field = "createdAt",
                GreaterThanOrEqualTo = fromDate
            });
        }

        if (!string.IsNullOrEmpty(toDate))
        {
            filters.Add(new DateRangeQuery
            {
                Field = "createdAt",
                LessThanOrEqualTo = toDate
            });
        }

        var query = new SearchDescriptor<TransactionCreatedEvent>()
            .Index("transaction_history")
            .Query(q => q
                .Bool(b => b
                    .Filter(filters.ToArray())
                )
            )
            .Collapse(c => c
                .Field("transferId.keyword")
            )
            .Sort(s => s
                .Field(f => f
                    .Field(doc => doc.CreatedAt)
                    .Order(SortOrder.Descending)
                )
            );

        var response = await elasticsearchClient.SearchAsync<TransactionCreatedEvent>(query);

        // Log the query and response for debugging
        Console.WriteLine($"Generated Query: {query}");
        Console.WriteLine($"Elasticsearch Response: {response.DebugInformation}");

        if (!response.IsValid)
        {
            return StatusCode(500, $"Elasticsearch query failed: {response.DebugInformation}");
        }

        var transactions = response.Documents
            .Select(d => new TransactionResponse
            {
                TransactionId = d.TransferId,
                FromAccount = d.FromAccount,
                ToAccount = d.ToAccount,
                TransactionAmount = d.Amount,
                Description = d.Description,
                Timestamp = d.CreatedAt
            })
            .ToList();

        return new OkObjectResult(transactions);
    }

    public class TransactionResponse
    {
        public string TransactionId { get; set; }
        public string FromAccount { get; set; }
        public string ToAccount { get; set; }
        public decimal TransactionAmount { get; set; }
        public string Description { get; set; }
        public string Timestamp { get; set; }
    }
}