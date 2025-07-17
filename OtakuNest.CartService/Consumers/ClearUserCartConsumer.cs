using MassTransit;
using OtakuNest.CartService.Services;
using OtakuNest.Contracts;

namespace OtakuNest.CartService.Consumers
{
    public class ClearUserCartConsumer : IConsumer<ClearUserCartEvent>
    {
        private readonly ICartService _cartService;

        public ClearUserCartConsumer(ICartService cartService)
        {
            _cartService = cartService;
        }

        public async Task Consume(ConsumeContext<ClearUserCartEvent> context)
        {
            var userId = context.Message.UserId;
            await _cartService.ClearCartAsync(userId, context.CancellationToken);
        }
    }
}
