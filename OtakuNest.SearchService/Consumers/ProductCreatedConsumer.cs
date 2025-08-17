using MassTransit;
using OtakuNest.Contracts;
using OtakuNest.SearchService.DTOs;
using OtakuNest.SearchService.Services;

namespace OtakuNest.SearchService.Consumers
{
    public class ProductCreatedConsumer : IConsumer<ProductCreatedEvent>
    {
        private readonly IElasticService _elasticService;

        public ProductCreatedConsumer(IElasticService elasticService)
        {
            _elasticService = elasticService;
        }

        public async Task Consume(ConsumeContext<ProductCreatedEvent> context)
        {
            var message = context.Message;

            var productElasticDto = new ProductElasticDto
            {
                Id = message.Id,
                Name = message.Name,
                Price = message.Price,
                ImageUrl = message.ImageUrl
            };

            await _elasticService.IndexProductAsync(productElasticDto);
        }
    }
}
