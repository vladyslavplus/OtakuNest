namespace OtakuNest.Contracts
{
    public record CartItemRemovedEvent(
        Guid UserId,
        Guid ProductId,
        DateTime RemovedAt);
}
