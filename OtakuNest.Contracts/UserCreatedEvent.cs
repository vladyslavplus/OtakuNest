namespace OtakuNest.Contracts
{
    public record UserCreatedEvent(
        Guid Id,
        string UserName,
        string Email,
        DateTime CreatedAt);
}
