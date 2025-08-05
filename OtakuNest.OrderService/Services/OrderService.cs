using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;
using OtakuNest.Contracts;
using OtakuNest.OrderService.Data;
using OtakuNest.OrderService.DTOs;
using OtakuNest.OrderService.Models;
using OtakuNest.OrderService.Parameters;

namespace OtakuNest.OrderService.Services
{
    public class OrderService : IOrderService
    {
        private readonly OrdersDbContext _dbContext;
        private readonly IRequestClient<CheckProductPriceRequest> _priceRequestClient;
        private readonly IRequestClient<CheckProductQuantityRequest> _quantityRequestClient;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ISortHelper<Order> _sortHelper;
        public OrderService(
            OrdersDbContext dbContext,
            IRequestClient<CheckProductPriceRequest> priceRequestClient,
            IRequestClient<CheckProductQuantityRequest> quantityRequestClient,
            IPublishEndpoint publishEndpoint,
            ISortHelper<Order> sortHelper)
        {
            _dbContext = dbContext;
            _priceRequestClient = priceRequestClient;
            _quantityRequestClient = quantityRequestClient;
            _publishEndpoint = publishEndpoint;
            _sortHelper = sortHelper;
        }

        public async Task<PagedList<OrderDto>> GetAllOrdersAsync(
            OrderParameters parameters,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.Orders
                .Include(o => o.Items)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(parameters.Status)
                && Enum.TryParse<OrderStatus>(parameters.Status, true, out var parsedStatus))
            {
                query = query.Where(o => o.Status == parsedStatus);
            }

            if (parameters.UserId.HasValue)
                query = query.Where(o => o.UserId == parameters.UserId.Value);

            if (parameters.MinPrice.HasValue)
                query = query.Where(o => o.TotalPrice >= parameters.MinPrice.Value);

            if (parameters.MaxPrice.HasValue)
                query = query.Where(o => o.TotalPrice <= parameters.MaxPrice.Value);

            if (parameters.FromDate.HasValue)
                query = query.Where(o => o.CreatedAt >= parameters.FromDate.Value);

            if (parameters.ToDate.HasValue)
                query = query.Where(o => o.CreatedAt <= parameters.ToDate.Value);

            if (parameters.ProductId.HasValue)
                query = query.Where(o => o.Items.Any(i => i.ProductId == parameters.ProductId.Value));

            query = _sortHelper.ApplySort(query, parameters.OrderBy);

            var pagedOrders = await PagedList<Order>.ToPagedListAsync(
                query.AsNoTracking(),
                parameters.PageNumber,
                parameters.PageSize,
                cancellationToken
            );

            var dtoList = pagedOrders.Select(MapToDto).ToList();

            return new PagedList<OrderDto>(dtoList, pagedOrders.TotalCount, parameters.PageNumber, parameters.PageSize);
        }

        public async Task<OrderDto?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            return order is null ? null : MapToDto(order);
        }

        public async Task<PagedList<OrderDto>> GetUserOrdersAsync(
            OrderParameters parameters,
            CancellationToken cancellationToken = default)
        {
            if (parameters.UserId is null)
                throw new ArgumentException("UserId must be provided to get user orders.");

            var query = _dbContext.Orders
                .Include(o => o.Items)
                .Where(o => o.UserId == parameters.UserId.Value)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(parameters.Status)
                && Enum.TryParse<OrderStatus>(parameters.Status, true, out var parsedStatus))
            {
                query = query.Where(o => o.Status == parsedStatus);
            }

            if (parameters.MinPrice.HasValue)
                query = query.Where(o => o.TotalPrice >= parameters.MinPrice.Value);

            if (parameters.MaxPrice.HasValue)
                query = query.Where(o => o.TotalPrice <= parameters.MaxPrice.Value);

            if (parameters.FromDate.HasValue)
                query = query.Where(o => o.CreatedAt >= parameters.FromDate.Value);

            if (parameters.ToDate.HasValue)
                query = query.Where(o => o.CreatedAt <= parameters.ToDate.Value);

            if (parameters.ProductId.HasValue)
                query = query.Where(o => o.Items.Any(i => i.ProductId == parameters.ProductId.Value));

            query = _sortHelper.ApplySort(query, parameters.OrderBy);

            var pagedOrders = await PagedList<Order>.ToPagedListAsync(
                query.AsNoTracking(),
                parameters.PageNumber,
                parameters.PageSize,
                cancellationToken
            );

            var dtoList = pagedOrders.Select(MapToDto).ToList();

            return new PagedList<OrderDto>(dtoList, pagedOrders.TotalCount, parameters.PageNumber, parameters.PageSize);
        }


        public async Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderDto dto, CancellationToken cancellationToken = default)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                ShippingAddress = dto.ShippingAddress
            };

            foreach (var itemDto in dto.Items)
            {
                var quantityResponse = await _quantityRequestClient.GetResponse<CheckProductQuantityResponse>(
                    new CheckProductQuantityRequest(itemDto.ProductId), cancellationToken);

                if (itemDto.Quantity > quantityResponse.Message.AvailableQuantity)
                {
                    throw new InvalidOperationException($"Not enough stock for product {itemDto.ProductId}. Requested: {itemDto.Quantity}, Available: {quantityResponse.Message.AvailableQuantity}");
                }

                var priceResponse = await _priceRequestClient.GetResponse<CheckProductPriceResponse>(
                    new CheckProductPriceRequest(itemDto.ProductId), cancellationToken);

                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = priceResponse.Message.Price,
                    OrderId = order.Id,
                    Order = order
                };

                order.Items.Add(orderItem);
            }

            foreach (var item in order.Items)
                await _publishEndpoint.Publish(new ProductQuantityUpdatedEvent(item.ProductId, -item.Quantity), cancellationToken);

            order.TotalPrice = order.Items.Sum(i => i.TotalPrice);

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _publishEndpoint.Publish(new ClearUserCartEvent(userId), cancellationToken);

            return MapToDto(order);
        }

        public async Task<bool> UpdateOrderStatusAsync(Guid orderId, string statusString, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(statusString)
                || !Enum.TryParse<OrderStatus>(statusString, true, out var newStatus))
            {
                return false;
            }

            var order = await _dbContext.Orders.FindAsync(new object[] { orderId }, cancellationToken);
            if (order == null)
                return false;

            var oldStatus = order.Status;
            order.Status = newStatus;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (newStatus == OrderStatus.Delivered && oldStatus != OrderStatus.Delivered)
                await _publishEndpoint.Publish(new OrderDeliveredEvent(order.Id, order.UserId, DateTime.UtcNow), cancellationToken);

            return true;
        }

        public async Task<bool> DeleteOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order == null)
                return false;

            if (order.Status != OrderStatus.Delivered)
            {
                foreach (var item in order.Items)
                    await _publishEndpoint.Publish(new ProductQuantityUpdatedEvent(item.ProductId, item.Quantity), cancellationToken);
            }

            _dbContext.Orders.Remove(order);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _publishEndpoint.Publish(new OrderDeletedEvent(order.Id, order.UserId, DateTime.UtcNow), cancellationToken);

            return true;
        }

        private static OrderDto MapToDto(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                UserId = order.UserId,
                CreatedAt = order.CreatedAt,
                Status = order.Status.ToString(),
                ShippingAddress = order.ShippingAddress,
                TotalPrice = order.TotalPrice,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList()
            };
        }
    }
}
