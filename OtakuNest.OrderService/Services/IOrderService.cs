using OtakuNest.Common.Helpers;
using OtakuNest.OrderService.DTOs;
using OtakuNest.OrderService.Parameters;

namespace OtakuNest.OrderService.Services
{
    public interface IOrderService
    {
        Task<PagedList<OrderDto>> GetAllOrdersAsync(OrderParameters parameters, CancellationToken cancellationToken = default);
        Task<OrderDto?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
        Task<List<OrderDto>> GetOrdersByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateOrderStatusAsync(Guid orderId, string statusString, CancellationToken cancellationToken = default);
        Task<bool> DeleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    }
}
