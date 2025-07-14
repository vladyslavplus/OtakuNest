using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.Contracts;
using OtakuNest.ProductService.Data;
using OtakuNest.ProductService.DTOs;
using OtakuNest.ProductService.Models;

namespace OtakuNest.ProductService.Services;

public class ProductService : IProductService
{
    private readonly ProductDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;

    public ProductService(ProductDbContext context, IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Select(p => MapToDto(p))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        return product is null ? null : MapToDto(product);
    }

    public async Task<ProductDto> CreateAsync(ProductCreateDto dto, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            Quantity = dto.Quantity,
            ImageUrl = dto.ImageUrl,
            Category = dto.Category,
            SKU = dto.SKU,
            IsAvailable = dto.IsAvailable,
            Rating = dto.Rating,
            Tags = dto.Tags,
            Discount = dto.Discount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(new ProductCreatedEvent(
            product.Id,
            product.Name,
            product.Price,
            product.SKU,
            product.Category,
            product.Quantity,
            product.IsAvailable,
            product.Discount,
            product.CreatedAt), cancellationToken);

        return MapToDto(product);
    }

    public async Task<bool> UpdateAsync(Guid id, ProductUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product is null) return false;

        if (dto.Name != null) product.Name = dto.Name;
        if (dto.Description != null) product.Description = dto.Description;
        if (dto.Price.HasValue) product.Price = dto.Price.Value;
        if (dto.Quantity.HasValue) product.Quantity = dto.Quantity.Value;
        if (dto.ImageUrl != null) product.ImageUrl = dto.ImageUrl;
        if (dto.Category != null) product.Category = dto.Category;
        if (dto.SKU != null) product.SKU = dto.SKU;
        if (dto.IsAvailable.HasValue) product.IsAvailable = dto.IsAvailable.Value;
        if (dto.Rating.HasValue) product.Rating = dto.Rating.Value;
        if (dto.Tags != null) product.Tags = dto.Tags;
        if (dto.Discount.HasValue) product.Discount = dto.Discount.Value;

        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product is null) return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ProductDto MapToDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Price = p.Price,
        Quantity = p.Quantity,
        ImageUrl = p.ImageUrl,
        Category = p.Category,
        SKU = p.SKU,
        IsAvailable = p.IsAvailable,
        Rating = p.Rating,
        Tags = p.Tags,
        Discount = p.Discount
    };
}
