using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.CartService.Data;
using OtakuNest.CartService.DTOs;
using OtakuNest.CartService.Exceptions;
using OtakuNest.CartService.Models;
using OtakuNest.Contracts;

namespace OtakuNest.CartService.Services
{
    public class CartService : ICartService
    {
        private readonly CartDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IRequestClient<CheckProductQuantityRequest> _quantityClient;

        public CartService(
            CartDbContext context,
            IPublishEndpoint publishEndpoint,
            IRequestClient<CheckProductQuantityRequest> quantityClient)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _quantityClient = quantityClient;
        }

        public async Task<CartDto> GetCartAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (cart == null)
                return new CartDto();

            return new CartDto
            {
                Items = cart.Items.Select(i => new CartItemDto
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList()
            };
        }

        public async Task AddItemToCartAsync(Guid userId, CartItemDto itemDto, CancellationToken cancellationToken = default)
        {
            var response = await _quantityClient
                .GetResponse<CheckProductQuantityResponse>(new CheckProductQuantityRequest(itemDto.ProductId), cancellationToken);

            var availableQuantity = response.Message.AvailableQuantity;

            if (availableQuantity < itemDto.Quantity)
                throw new NotEnoughStockException(itemDto.ProductId, itemDto.Quantity, availableQuantity);

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
            }

            var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == itemDto.ProductId);

            if (existingItem != null)
            {
                var newQuantity = existingItem.Quantity + itemDto.Quantity;

                if (newQuantity > availableQuantity)
                    throw new NotEnoughStockException(itemDto.ProductId, itemDto.Quantity, availableQuantity);

                existingItem.Quantity = newQuantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            var cartItemAdded = new CartItemAddedEvent(
                userId,
                itemDto.ProductId,
                itemDto.Quantity,
                DateTime.UtcNow);

            await _publishEndpoint.Publish(cartItemAdded, cancellationToken);
        }

        public async Task RemoveItemFromCartAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (cart == null) return;

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                cart.Items.Remove(item);
                await _context.SaveChangesAsync(cancellationToken);

                var itemRemoved = new CartItemRemovedEvent(
                    userId,
                    productId,
                    DateTime.UtcNow);

                await _publishEndpoint.Publish(itemRemoved, cancellationToken);
            }
        }

        public async Task ClearCartAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (cart != null && cart.Items.Any())
            {
                cart.Items.Clear();
                await _context.SaveChangesAsync(cancellationToken);

                var cartCleared = new CartClearedEvent(
                    userId,
                    DateTime.UtcNow);

                await _publishEndpoint.Publish(cartCleared, cancellationToken);
            }
        }

        public async Task ChangeItemQuantityAsync(Guid userId, Guid productId, int delta, CancellationToken cancellationToken = default)
        {
            if (delta == 0) return;

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

            if (cart == null) return;

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null) return;

            var newQuantity = item.Quantity + delta;

            if (newQuantity <= 0)
            {
                cart.Items.Remove(item);

                await _context.SaveChangesAsync(cancellationToken);

                await _publishEndpoint.Publish(new CartItemRemovedEvent(
                    userId,
                    productId,
                    DateTime.UtcNow), cancellationToken);

                return;
            }

            if (delta > 0)
            {
                var response = await _quantityClient
                    .GetResponse<CheckProductQuantityResponse>(new CheckProductQuantityRequest(productId), cancellationToken);

                var availableQuantity = response.Message.AvailableQuantity;

                if (newQuantity > availableQuantity)
                    throw new NotEnoughStockException(productId, newQuantity, availableQuantity);
            }

            item.Quantity = newQuantity;

            await _context.SaveChangesAsync(cancellationToken);

            await _publishEndpoint.Publish(new CartItemQuantityChangedEvent(
                userId,
                productId,
                item.Quantity,
                DateTime.UtcNow), cancellationToken);
        }
    }
}

