using OtakuNest.Common.Parameters;

namespace OtakuNest.ProductService.Parameters
{
    public class ProductParameters : QueryStringParameters
    {
        public string? Name { get; set; }
        public string? Category { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public bool? IsAvailable { get; set; }
        public string? SKU { get; set; }

        public double? MinRating { get; set; }
        public double? MaxRating { get; set; }

        public decimal? MinDiscount { get; set; }
        public decimal? MaxDiscount { get; set; }
    }
}
