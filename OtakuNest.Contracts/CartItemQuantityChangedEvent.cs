namespace OtakuNest.Contracts
{
    public record CartItemQuantityChangedEvent(
        Guid UserId,
        Guid ProductId,
        int NewQuantity,
        DateTime ChangedAt);
}
