using OtakuNest.CartService.DTOs;

namespace OtakuNest.CartService.Services
{
    public interface ICartService
    {
        Task<CartDto> GetCartAsync(Guid userId, CancellationToken cancellationToken = default);
        Task AddItemToCartAsync(Guid userId, CartItemDto itemDto, CancellationToken cancellationToken = default);
        Task RemoveItemFromCartAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default);
        Task ClearCartAsync(Guid userId, CancellationToken cancellationToken = default);
        Task ChangeItemQuantityAsync(Guid userId, Guid productId, int delta, CancellationToken cancellationToken = default); // 👈 new
    }
}
