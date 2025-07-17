namespace OtakuNest.Contracts
{
    public record ProductQuantityUpdatedEvent(Guid ProductId, int QuantityChange);
}
