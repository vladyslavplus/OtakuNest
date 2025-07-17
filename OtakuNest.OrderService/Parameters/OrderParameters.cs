using OtakuNest.Common.Parameters;
using OtakuNest.OrderService.Models;

namespace OtakuNest.OrderService.Parameters
{
    public class OrderParameters : QueryStringParameters
    {
        public string? Status { get; set; }
        public Guid? UserId { get; set; }

        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public Guid? ProductId { get; set; }
    }
}
