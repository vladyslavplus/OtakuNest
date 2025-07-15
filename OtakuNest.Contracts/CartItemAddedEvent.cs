namespace OtakuNest.Contracts
{
    public record CartItemAddedEvent(
        Guid UserId,
        Guid ProductId,
        int Quantity,
        DateTime AddedAt);
}
