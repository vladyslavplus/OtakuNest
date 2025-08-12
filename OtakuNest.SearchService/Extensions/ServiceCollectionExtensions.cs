using Elastic.Clients.Elasticsearch;
using OtakuNest.SearchService.Services;

namespace OtakuNest.SearchService.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddElasticSearch(this IServiceCollection services, string elasticSearchUrl)
        {
            var settings = new ElasticsearchClientSettings(new Uri(elasticSearchUrl))
                .DefaultIndex("products");

            var client = new ElasticsearchClient(settings);

            services.AddSingleton(client);
            services.AddScoped<IElasticService, ElasticService>();

            return services;
        }
    }
}
