namespace OtakuNest.Contracts
{
    public record ProductCreatedEvent(
    Guid Id,
    string Name,
    decimal Price,
    string SKU,
    string Category,
    int Quantity,
    bool IsAvailable,
    decimal Discount,
    DateTime CreatedAt);
}
