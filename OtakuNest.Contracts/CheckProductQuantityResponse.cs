namespace OtakuNest.Contracts
{
    public record CheckProductQuantityResponse(Guid ProductId, int AvailableQuantity);
}
