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
            var message = context.Message;

            var product = new ProductElasticDto
            {
                Id = message.Id,
                Name = message.Name,
                Price = message.Price,
                ImageUrl = message.ImageUrl,
            };

            await _elasticService.UpdateProductAsync(product);
        }
    }
}
