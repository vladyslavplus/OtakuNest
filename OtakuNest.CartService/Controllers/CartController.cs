using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OtakuNest.CartService.DTOs;
using OtakuNest.CartService.Extensions;
using OtakuNest.CartService.Services;

namespace OtakuNest.CartService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var userId = User.GetUserId();
            var cart = await _cartService.GetCartAsync(userId);
            return Ok(cart);
        }

        [HttpPost]
        public async Task<IActionResult> AddItem([FromBody] AddCartItemDto dto)
        {
            var userId = User.GetUserId();
            var itemDto = new CartItemDto
            {
                ProductId = dto.ProductId,
                Quantity = dto.Quantity
            };

            await _cartService.AddItemToCartAsync(userId, itemDto);
            return Ok();
        }

        [HttpDelete("{productId:guid}")]
        public async Task<IActionResult> RemoveItem(Guid productId)
        {
            var userId = User.GetUserId();
            await _cartService.RemoveItemFromCartAsync(userId, productId);
            return NoContent();
        }

        [HttpDelete("clear")]
        public async Task<IActionResult> ClearCart()
        {
            var userId = User.GetUserId();
            await _cartService.ClearCartAsync(userId);
            return NoContent();
        }

        [HttpPatch("quantity")]
        public async Task<IActionResult> ChangeQuantity([FromBody] UpdateCartItemQuantityDto dto)
        {
            if (dto.Delta == 0)
                return BadRequest("Delta must not be 0.");

            var userId = User.GetUserId();
            await _cartService.ChangeItemQuantityAsync(userId, dto.ProductId, dto.Delta);
            return Ok();
        }
    }
}
