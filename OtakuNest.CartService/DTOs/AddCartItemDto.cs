using System.ComponentModel.DataAnnotations;

namespace OtakuNest.CartService.DTOs
{
    public class AddCartItemDto
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
