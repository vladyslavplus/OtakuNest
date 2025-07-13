namespace OtakuNest.ProductService.DTOs
{
    public class ProductCreateDto
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string SKU { get; set; } = null!;
        public bool IsAvailable { get; set; } = true;
        public double Rating { get; set; } = 0.0;
        public string Tags { get; set; } = null!;
        public decimal Discount { get; set; } = 0m;
    }
}
