using MassTransit;
using OtakuNest.Contracts;
using OtakuNest.ProductService.Data;

namespace OtakuNest.ProductService.Consumers
{
    public class ProductQuantityUpdatedConsumer : IConsumer<ProductQuantityUpdatedEvent>
    {
        private readonly ProductDbContext _dbContext;

        public ProductQuantityUpdatedConsumer(ProductDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task Consume(ConsumeContext<ProductQuantityUpdatedEvent> context)
        {
            var message = context.Message;
            var product = await _dbContext.Products.FindAsync(message.ProductId);
            if (product == null)
            {
                return;
            }

            product.Quantity += message.QuantityChange;

            if (product.Quantity < 0)
                product.Quantity = 0;

            await _dbContext.SaveChangesAsync();
        }
    }
}
