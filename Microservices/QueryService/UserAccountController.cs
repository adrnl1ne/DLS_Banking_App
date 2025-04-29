using Microsoft.AspNetCore.Mvc;
using Nest;
using QueryService.DTO;
using System.Threading.Tasks;

namespace QueryService
{
    [ApiController]
    [Route("api/user/")]
    public class UserAccountController : ControllerBase
    {
        private readonly IElasticClient _elasticsearchClient;

        public UserAccountController(IElasticClient elasticsearchClient)
        {
            _elasticsearchClient = elasticsearchClient;
        }

        [HttpGet("accounts/{userId}")]
        public async Task<IActionResult> GetUserAccounts(int userId)
        {
            var response = await _elasticsearchClient.SearchAsync<AccountCreatedEvent>(s => s
                .Index("account_created")
                .Query(q => q
                    .Term(t => t
                        .Field(f => f.UserId)
                        .Value(userId)
                    )
                )
                .Sort(sort => sort
                    .Descending(f => f.Timestamp)
                )
            );

            if (!response.IsValid)
            {
                return BadRequest(response.ServerError?.ToString() ?? "Unknown Elasticsearch error.");
            }
            
            var accounts = response.Documents.Select(doc => new Account
            {
                id = doc.AccountId,
                name = doc.Name,
                amount = doc.Amount,
                userId = doc.UserId
            });

            return Ok(accounts);
        }


    }
}