using MassTransit;
using OtakuNest.Contracts;
using OtakuNest.ProductService.Services;

namespace OtakuNest.ProductService.Consumers
{
    public class CheckProductQuantityConsumer : IConsumer<CheckProductQuantityRequest>
    {
        private readonly IProductService _productService;

        public CheckProductQuantityConsumer(IProductService productService)
        {
            _productService = productService;
        }

        public async Task Consume(ConsumeContext<CheckProductQuantityRequest> context)
        {
            var product = await _productService.GetByIdAsync(context.Message.ProductId);

            var quantity = product?.Quantity ?? 0;

            await context.RespondAsync(new CheckProductQuantityResponse(
                ProductId: context.Message.ProductId,
                AvailableQuantity: quantity
            ));
        }
    }
}
