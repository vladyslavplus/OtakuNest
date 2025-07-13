﻿namespace OtakuNest.ProductService.DTOs
{
    public class ProductUpdateDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        public string? SKU { get; set; }
        public bool? IsAvailable { get; set; }
        public double? Rating { get; set; }
        public string? Tags { get; set; }
        public decimal? Discount { get; set; }
    }
}
