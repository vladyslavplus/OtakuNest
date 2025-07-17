using MassTransit;
using OtakuNest.Contracts;
using OtakuNest.ProductService.Data;

namespace OtakuNest.ProductService.Consumers
{
    public class CheckProductPriceConsumer : IConsumer<CheckProductPriceRequest>
    {
        private readonly ProductDbContext _dbContext;

        public CheckProductPriceConsumer(ProductDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Consume(ConsumeContext<CheckProductPriceRequest> context)
        {
            var product = await _dbContext.Products.FindAsync(context.Message.ProductId);

            if (product == null)
            {
                await context.RespondAsync(new CheckProductPriceResponse(context.Message.ProductId, 0m));
                return;
            }

            await context.RespondAsync(new CheckProductPriceResponse(context.Message.ProductId, product.Price));
        }
    }
}
