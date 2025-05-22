

using System.Transactions;
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
                else if (type == typeof(CheckFraudEvent))
                {
                    await CreateIndexGeneric<CheckFraudEvent>(elasticClient, indexName);
                }
                else if (type == typeof(TransactionCreatedEvent))
                {
                    await CreateIndexGeneric<TransactionCreatedEvent>(elasticClient, indexName);
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
            .Map<T>(m => m.AutoMap())
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

}
