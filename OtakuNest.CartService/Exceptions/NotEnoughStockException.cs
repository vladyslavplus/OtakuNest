namespace OtakuNest.CartService.Exceptions
{
    public class NotEnoughStockException : Exception
    {
        public NotEnoughStockException(Guid productId, int requested, int available)
            : base($"Not enough stock for product {productId}. Requested: {requested}, Available: {available}")
        {
            ProductId = productId;
            Requested = requested;
            Available = available;
        }

        public Guid ProductId { get; }
        public int Requested { get; }
        public int Available { get; }
    }
}
