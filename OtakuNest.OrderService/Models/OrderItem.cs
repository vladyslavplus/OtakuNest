namespace OtakuNest.OrderService.Models
{
    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;

        public Guid OrderId { get; set; }
        public Order? Order { get; set; }
    }
}
