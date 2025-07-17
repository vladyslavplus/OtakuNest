namespace OtakuNest.Contracts
{
    public record OrderDeliveredEvent(Guid OrderId, Guid UserId, DateTime DeliveredAt);
}
