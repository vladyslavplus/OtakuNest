namespace OtakuNest.OrderService.Models
{
    public class Order
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public OrderStatus Status { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public string ShippingAddress { get; set; } = null!;
    }
}
