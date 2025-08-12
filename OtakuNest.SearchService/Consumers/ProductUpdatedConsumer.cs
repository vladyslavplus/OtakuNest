using MassTransit;
using OtakuNest.Contracts;
using OtakuNest.SearchService.DTOs;
using OtakuNest.SearchService.Services;

namespace OtakuNest.SearchService.Consumers
{
    public class ProductUpdatedConsumer : IConsumer<ProductUpdatedEvent>
    {
        private readonly IElasticService _elasticService;

        public ProductUpdatedConsumer(IElasticService elasticService)
        {
            _elasticService = elasticService;
        }

        public async Task Consume(ConsumeContext<ProductUpdatedEvent> context)
        {
            var product = new ProductElasticDto
            {
                Id = context.Message.Id,
                Name = context.Message.Name
            };

            await _elasticService.UpdateProductAsync(product);
        }
    }
}
