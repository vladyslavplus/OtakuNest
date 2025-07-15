namespace OtakuNest.CartService.DTOs
{
    public class CartDto
    {
        public List<CartItemDto> Items { get; set; } = new();
    }
}
