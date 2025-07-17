namespace OtakuNest.OrderService.DTOs
{
    public class CreateOrderDto
    {
        public string ShippingAddress { get; set; } = null!;
        public List<CreateOrderItemDto> Items { get; set; } = new();
    }
}
