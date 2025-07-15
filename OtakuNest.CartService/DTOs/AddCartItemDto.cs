using System.ComponentModel.DataAnnotations;

namespace OtakuNest.CartService.DTOs
{
    public class AddCartItemDto
    {
        public Guid ProductId { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; } = 1;
    }
}
