using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OtakuNest.Common.Extensions;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;
using OtakuNest.OrderService.DTOs;
using OtakuNest.OrderService.Extensions;
using OtakuNest.OrderService.Models;
using OtakuNest.OrderService.Parameters;
using OtakuNest.OrderService.Services;

namespace OtakuNest.OrderService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ISortHelper<Order> _sortHelper;

        public OrdersController(IOrderService orderService, ISortHelper<Order> sortHelper)
        {
            _orderService = orderService;
            _sortHelper = sortHelper;
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PagedList<OrderDto>>> GetAllOrders([FromQuery] OrderParameters parameters, CancellationToken cancellationToken)
        {
            var orders = await _orderService.GetAllOrdersAsync(parameters, _sortHelper, cancellationToken);
            Response.AddPaginationHeader(orders);
            return Ok(orders);
        }

        [HttpGet("{orderId:guid}")]
        [Authorize]
        public async Task<ActionResult<OrderDto>> GetOrderById(Guid orderId, CancellationToken cancellationToken)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId, cancellationToken);
            return order is null ? NotFound() : Ok(order);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<List<OrderDto>>> GetMyOrders(CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var orders = await _orderService.GetOrdersByUserIdAsync(userId, cancellationToken);
            return Ok(orders);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto createOrderDto, CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();
            var createdOrder = await _orderService.CreateOrderAsync(userId, createOrderDto, cancellationToken);
            return CreatedAtAction(nameof(GetOrderById), new { orderId = createdOrder.Id }, createdOrder);
        }

        [HttpPut("{orderId:guid}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] OrderStatusUpdateDto statusUpdate, CancellationToken cancellationToken)
        {
            var updated = await _orderService.UpdateOrderStatusAsync(orderId, statusUpdate.Status, cancellationToken);
            return !updated ? BadRequest("Invalid order status or order not found.") : NoContent();
        }

        [HttpDelete("{orderId:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteOrder(Guid orderId, CancellationToken cancellationToken)
        {
            var deleted = await _orderService.DeleteOrderAsync(orderId, cancellationToken);
            return !deleted ? NotFound() : NoContent();
        }
    }
}
