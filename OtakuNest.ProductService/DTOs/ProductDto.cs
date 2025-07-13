namespace OtakuNest.ProductService.DTOs
{
    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string SKU { get; set; } = null!;
        public bool IsAvailable { get; set; }
        public double Rating { get; set; }
        public string Tags { get; set; } = null!;
        public decimal Discount { get; set; }
    }
}
