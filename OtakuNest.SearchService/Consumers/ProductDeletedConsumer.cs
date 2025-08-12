using MassTransit;
using OtakuNest.Contracts;
using OtakuNest.SearchService.Services;

namespace OtakuNest.SearchService.Consumers
{
    public class ProductDeletedConsumer : IConsumer<ProductDeletedEvent>
    {
        private readonly IElasticService _elasticService;

        public ProductDeletedConsumer(IElasticService elasticService)
        {
            _elasticService = elasticService;
        }

        public async Task Consume(ConsumeContext<ProductDeletedEvent> context)
        {
            await _elasticService.DeleteProductAsync(context.Message.Id);
        }
    }
}
