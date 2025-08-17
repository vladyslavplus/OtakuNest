using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using OtakuNest.SearchService.DTOs;

namespace OtakuNest.SearchService.Services
{
    public class ElasticService : IElasticService
    {
        private readonly ElasticsearchClient _client;
        private readonly ILogger<ElasticService> _logger;
        private const string IndexName = "products";

        public ElasticService(ElasticsearchClient client, ILogger<ElasticService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task IndexProductAsync(ProductElasticDto product, CancellationToken cancellationToken = default)
        {
            var response = await _client.IndexAsync(product, idx => idx
                .Index(IndexName)
                .Id(product.Id), cancellationToken);

            if (!response.IsValidResponse)
            {
                var reason = response.ElasticsearchServerError?.Error.Reason ?? "Unknown error";
                _logger.LogError("Failed to index product {ProductId}: {Error}", product.Id, reason);
                throw new InvalidOperationException($"Failed to index product: {reason}");
            }

            _logger.LogInformation("Successfully indexed product {ProductId}", product.Id);
        }

        public async Task UpdateProductAsync(ProductElasticDto product, CancellationToken cancellationToken = default)
        {
            var response = await _client.UpdateAsync<ProductElasticDto, ProductElasticDto>(
                product.Id,
                u => u.Index(IndexName).Doc(product).DocAsUpsert(true),
                cancellationToken);

            if (!response.IsValidResponse)
            {
                var reason = response.ElasticsearchServerError?.Error.Reason ?? "Unknown error";
                _logger.LogError("Failed to update product {ProductId}: {Error}", product.Id, reason);
                throw new InvalidOperationException($"Failed to update product: {reason}");
            }

            _logger.LogInformation("Successfully updated product {ProductId}", product.Id);
        }

        public async Task DeleteProductAsync(Guid productId, CancellationToken cancellationToken = default)
        {
            var response = await _client.DeleteAsync<ProductElasticDto>(productId, d => d.Index(IndexName), cancellationToken);

            if (!response.IsValidResponse)
            {
                var reason = response.ElasticsearchServerError?.Error.Reason ?? "Unknown error";
                _logger.LogError("Failed to delete product {ProductId}: {Error}", productId, reason);
                throw new InvalidOperationException($"Failed to delete product: {reason}");
            }

            _logger.LogInformation("Successfully deleted product {ProductId}", productId);
        }

        public async Task<IEnumerable<ProductElasticDto>> SearchByNameAsync(
            string productName,
            int page = 1,
            int size = 20,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(productName))
                return Enumerable.Empty<ProductElasticDto>();

            var searchResponse = await _client.SearchAsync<ProductElasticDto>(s => s
                .Index(IndexName)
                .From((page - 1) * size)
                .Size(size)
                .Query(q => q
                    .Bool(b => b
                        .Should(
                            s => s.Term(t => t.Field(new Field("name.keyword")).Value(productName).Boost(5.0f)),

                            s => s.Match(m => m.Field(new Field("name")).Query(productName).Boost(4.0f)),

                            s => s.MultiMatch(mm => mm
                                .Fields(new Field("name"))
                                .Query(productName)
                                .Type(TextQueryType.BestFields)
                                .Boost(3.5f)
                            ),

                            s => s.MatchPhrasePrefix(mpp => mpp
                                .Field(new Field("name"))
                                .Query(productName)
                                .Boost(3.0f)
                            ),

                            s => s.Wildcard(w => w.Field(new Field("name")).Value($"*{productName.ToLower()}*").Boost(2.0f)),

                            s => s.Fuzzy(f => f.Field(new Field("name")).Value(productName).Fuzziness(new Fuzziness(2)).Boost(1.5f)),

                            s => s.QueryString(qs => qs
                                .Fields(new Field("name"))
                                .Query($"*{string.Join("* AND *", productName.Split(' ', StringSplitOptions.RemoveEmptyEntries))}*")
                                .Boost(1.0f)
                            )
                        )
                        .MinimumShouldMatch(1)
                    )
                )
                .Sort(so => so.Score(new ScoreSort { Order = SortOrder.Desc })),
                cancellationToken);

            if (!searchResponse.IsValidResponse)
            {
                var reason = searchResponse.ElasticsearchServerError?.Error.Reason ?? "Unknown error";
                _logger.LogError("Search by name failed for query '{Query}': {Error}", productName, reason);
                throw new InvalidOperationException($"Search failed: {reason}");
            }

            _logger.LogInformation("Search by name '{ProductName}' returned {Count} results",
                productName, searchResponse.Documents.Count);

            return searchResponse.Documents;
        }

        public async Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default)
        {
            var existsResponse = await _client.Indices.ExistsAsync(IndexName, cancellationToken);
            if (!existsResponse.Exists)
            {
                var createResponse = await _client.Indices.CreateAsync(IndexName, c => c
                    .Mappings(m => m
                        .Properties(new Properties
                        {
                            ["Id"] = new KeywordProperty(),
                            ["Name"] = new TextProperty
                            {
                                Fields = new Properties
                                {
                                    ["keyword"] = new KeywordProperty()
                                }
                            }
                        })
                    ),
                    cancellationToken);

                if (!createResponse.IsValidResponse)
                    throw new InvalidOperationException($"Failed to create index {IndexName}: {createResponse.ElasticsearchServerError?.Error.Reason}");
            }
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            var healthResponse = await _client.Cluster.HealthAsync(cancellationToken: cancellationToken);

            if (!healthResponse.IsValidResponse)
            {
                _logger.LogError("Cluster health check failed: {Error}", healthResponse.ElasticsearchServerError?.Error.Reason ?? "Unknown error");
                return false;
            }

            return healthResponse.Status == HealthStatus.Green || healthResponse.Status == HealthStatus.Yellow;
        }
    }
}
