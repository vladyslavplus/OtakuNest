namespace OtakuNest.Contracts
{
    public record OrderDeletedEvent(Guid OrderId, Guid UserId, DateTime DeletedAt);
}
