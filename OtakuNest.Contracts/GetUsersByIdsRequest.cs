namespace OtakuNest.Contracts
{
    public record GetUsersByIdsRequest(IEnumerable<Guid> UserIds);
}
