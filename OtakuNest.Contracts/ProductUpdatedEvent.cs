namespace OtakuNest.Contracts
{
    public record ProductUpdatedEvent(
        Guid Id,
        string Name,
        decimal Price,
        string ImageUrl,
        string SKU,
        string Category,
        int Quantity,
        bool IsAvailable,
        decimal Discount,
        DateTime UpdatedAt);
}
