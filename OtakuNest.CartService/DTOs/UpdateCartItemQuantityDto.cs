namespace OtakuNest.CartService.DTOs
{
    public class UpdateCartItemQuantityDto
    {
        public Guid ProductId { get; set; }
        public int Delta { get; set; } // +1 or -1
    }
}
