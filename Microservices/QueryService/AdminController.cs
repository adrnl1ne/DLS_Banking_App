using Microsoft.AspNetCore.Mvc;
using Nest;
using QueryService.DTO;

[ApiController]
[Route("api/admin/")]
public class AdminController : ControllerBase
{
    private readonly IElasticClient _elasticClient;

    public AdminController(IElasticClient elasticClient)
    {
        _elasticClient = elasticClient;
    }

    [HttpGet("users/search/{term}")]
    public async Task<IActionResult> GetUser(string term)
    {
        var response = await _elasticClient.SearchAsync<UserDocument>(s => s
            .Index("users")
            .Query(q => q
                .Bool(b => b
                    .Should(
                        q.Match(m => m.Field(f => f.Username).Query(term)),
                        q.Match(m => m.Field(f => f.Email).Query(term))
                    )
                    .MinimumShouldMatch(1)
                )
            )
        );

        if (!response.IsValid)
        {
            return BadRequest(response.ServerError?.ToString() ?? "Unknown Elasticsearch error.");
        }

        return Ok(response.Documents);
    }
}