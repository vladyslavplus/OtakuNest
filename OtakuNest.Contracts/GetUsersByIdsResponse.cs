namespace OtakuNest.Contracts
{
    public record GetUsersByIdsResponse(IEnumerable<UserShortInfo> Users);
}
