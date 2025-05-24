using System.Text.Json;
using Nest;
using QueryService.DTO;
using QueryService.utils;

public class Helpers
{
    public static async Task EnsureElasticsearchIndicesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var elasticClient = scope.ServiceProvider.GetRequiredService<IElasticClient>();

        foreach (var (indexName, type) in ES.indexMap)
        {
            var existsResponse = await elasticClient.Indices.ExistsAsync(indexName);
            if (!existsResponse.Exists)
            {
                // Call the CreateIndexGeneric method for the corresponding document type
                if (type == typeof(UserDocument))
                {
                    await CreateIndexGeneric<UserDocument>(elasticClient, indexName);
                }
                else if (type == typeof(TransactionDocument))
                {
                    await CreateIndexGeneric<TransactionDocument>(elasticClient, indexName);
                }
                else if (type == typeof(AccountCreatedEvent))
                {
                    await CreateIndexGeneric<AccountCreatedEvent>(elasticClient, indexName);
                }
                else if (type == typeof(AccountEvent))
                {
                    await CreateIndexGeneric<AccountEvent>(elasticClient, indexName);
                }
                else if (type == typeof(CheckFraudEvent))
                {
                    await CreateIndexGeneric<CheckFraudEvent>(elasticClient, indexName);
                }
                else if (type == typeof(TransactionCreatedEvent))
                {
                    await CreateIndexGeneric<TransactionCreatedEvent>(elasticClient, indexName);
                }
                else if (type == typeof(DeletedAccount))
                {
                    await CreateIndexGeneric<DeletedAccount>(elasticClient, indexName);
                }
                else
                {
                    Console.WriteLine($"❌ Unknown type for index: {indexName}");
                }
            }
            else
            {
                Console.WriteLine($"ℹ️ Index '{indexName}' already exists.");
            }
        }
    }

// Method to create an index for the specified document type
    public static async Task CreateIndexGeneric<T>(IElasticClient client, string indexName) where T : class
    {
        var response = await client.Indices.CreateAsync(indexName, c => c
            .Map<T>(m => m
                .AutoMap()
                // Add specific mapping for timestamp field to make it sortable
                .Properties(p => p
                    .Keyword(k => k
                        .Name("timestamp")
                    )
                )
            )
        );

        if (response.IsValid)
        {
            Console.WriteLine($"✅ Created index: {indexName}");
        }
        else
        {
            Console.WriteLine($"❌ Failed to create index '{indexName}': {response.DebugInformation}");
        }
    }

    // DTO for deserializing the account API response
    private class AccountApiResponse
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? Name { get; set; }
        public decimal Amount { get; set; }
    }

    public static async Task LoadInitialAccountDataAsync(IServiceProvider services)
    {
        Console.WriteLine("🔄 Attempting to load initial account data...");
        
        try {
            using var scope = services.CreateScope();
            var elasticClient = scope.ServiceProvider.GetRequiredService<IElasticClient>();
            
            // Use the configured HTTP client with auth headers
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("ServiceClient");
            
            // Configure for your environment - use the new service-specific endpoint
            var url = "/api/account/all";
            
            Console.WriteLine($"📡 Requesting data from: {httpClient.BaseAddress}{url}");
            
            var response = await httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"📥 Raw API response: {content}");
                
                var accountsFromApi = JsonSerializer.Deserialize<List<AccountApiResponse>>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                Console.WriteLine($"📥 Retrieved {accountsFromApi?.Count ?? 0} accounts from service");
                
                if (accountsFromApi != null && accountsFromApi.Any())
                {
                    foreach (var apiAccount in accountsFromApi)
                    {
                        // Create AccountEvent from API response
                        var accountEvent = new AccountEvent 
                        {
                            EventType = "AccountCreated",
                            AccountId = apiAccount.Id,
                            UserId = apiAccount.UserId,
                            Name = apiAccount.Name ?? "Account",
                            Amount = apiAccount.Amount,
                            Timestamp = DateTime.UtcNow.ToString("o")
                        };
                        
                        var indexResponse = await elasticClient.IndexAsync(accountEvent, 
                            idx => idx.Index("accounts").Id(accountEvent.AccountId));
                            
                        Console.WriteLine(indexResponse.IsValid
                            ? $"✅ Imported account {accountEvent.AccountId} to Elasticsearch"
                            : $"❌ Failed to import account {accountEvent.AccountId}: {indexResponse.DebugInformation}");
                    }
                    
                    Console.WriteLine("✅ Initial account data loaded successfully");
                }
                else
                {
                    Console.WriteLine("⚠️ No accounts to import from UserAccountService");
                }
            }
            else
            {
                Console.WriteLine($"❌ Failed to fetch accounts: {response.StatusCode}");
                Console.WriteLine($"Response content: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error loading initial account data: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    // Additional utility method to update existing indices if needed
    public static async Task UpdateExistingIndex(IServiceProvider services, string indexName)
    {
        try
        {
            using var scope = services.CreateScope();
            var elasticClient = scope.ServiceProvider.GetRequiredService<IElasticClient>();
            
            Console.WriteLine($"🔄 Attempting to update mapping for {indexName}...");
            
            // Check if the index exists
            var existsResponse = await elasticClient.Indices.ExistsAsync(indexName);
            if (!existsResponse.Exists)
            {
                Console.WriteLine($"❌ Index {indexName} does not exist");
                return;
            }
            
            // Update the mapping for timestamp field
            var updateResponse = await elasticClient.Indices.PutMappingAsync<AccountEvent>(m => m
                .Index(indexName)
                .Properties(p => p
                    .Keyword(k => k
                        .Name(n => n.Timestamp)
                    )
                )
            );
            
            if (updateResponse.IsValid)
            {
                Console.WriteLine($"✅ Successfully updated mapping for {indexName}");
            }
            else
            {
                Console.WriteLine($"❌ Failed to update mapping for {indexName}: {updateResponse.DebugInformation}");
                
                // If we can't update the mapping, let's try reindexing
                Console.WriteLine("⚠️ Attempting to reindex as an alternative...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error updating index: {ex.Message}");
        }
    }
}
