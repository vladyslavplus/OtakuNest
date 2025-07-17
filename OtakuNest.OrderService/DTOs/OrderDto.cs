namespace OtakuNest.OrderService.DTOs
{
    public class OrderDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = null!;
        public List<OrderItemDto> Items { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public string ShippingAddress { get; set; } = null!;
    }
}
