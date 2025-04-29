using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Nest;
using QueryService.DTO;

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
    public async Task<IActionResult> GetTransactionHistory(int accountId, [FromQuery] string fromDate = null, [FromQuery] string toDate = null)
    {
        var filters = new List<QueryContainer>();

        filters.Add(new TermQuery { Field = "accountId", Value = accountId });

        if (!string.IsNullOrEmpty(fromDate))
        {
            filters.Add(new DateRangeQuery
            {
                Field = "timestamp",
                GreaterThanOrEqualTo = fromDate
            });
        }

        if (!string.IsNullOrEmpty(toDate))
        {
            filters.Add(new DateRangeQuery
            {
                Field = "timestamp",
                LessThanOrEqualTo = toDate
            });
        }

        var query = new SearchDescriptor<TransactionDocument>()
            .Index("transactions")
            .Query(q => q
                .Bool(b => b
                    .Filter(filters.ToArray())
                )
            )
            .Sort(s => s
                .Field(f => f
                    .Field(doc => doc.Timestamp)
                    .Order(SortOrder.Descending)
                )
            );

        var response = await elasticsearchClient.SearchAsync<TransactionDocument>(query);

        var transactions = response.Documents
            .Select(d => new TransactionResponse
            {
                TransactionId = d.TransactionId,
                AccountId = d.AccountId,
                UserId = d.UserId,
                TransactionAmount = d.Amount,
                FinalBalance = d.FinalBalance,
                TransactionType = d.TransactionType,
                Timestamp = d.Timestamp
            })
            .ToList(); // IMPORTANT

        return new OkObjectResult(transactions);
    }



    public class TransactionResponse
    {
        public string TransactionId { get; set; }
        public int AccountId { get; set; }
        public int UserId { get; set; }
        public decimal TransactionAmount { get; set; }
        public decimal FinalBalance { get; set; }
        public string TransactionType { get; set; }
        public string Timestamp { get; set; }
    }
}