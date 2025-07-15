using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.CartService.Data;
using OtakuNest.CartService.Models;
using OtakuNest.Contracts;

namespace OtakuNest.CartService.Consumers
{
    public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
    {
        private readonly CartDbContext _context;

        public UserCreatedConsumer(CartDbContext context)
        {
            _context = context;
        }

        public async Task Consume(ConsumeContext<UserCreatedEvent> context)
        {
            var userId = context.Message.Id;

            var exists = await _context.Carts.AnyAsync(c => c.UserId == userId);
            if (!exists)
            {
                _context.Carts.Add(new Cart { UserId = userId });
                await _context.SaveChangesAsync();
            }
        }
    }
}
