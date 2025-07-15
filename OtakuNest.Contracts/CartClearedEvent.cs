namespace OtakuNest.Contracts
{
    public record CartClearedEvent(
        Guid UserId,
        DateTime ClearedAt);
}
