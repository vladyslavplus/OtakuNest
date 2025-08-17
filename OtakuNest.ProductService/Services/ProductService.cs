using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;
using OtakuNest.Contracts;
using OtakuNest.ProductService.Data;
using OtakuNest.ProductService.DTOs;
using OtakuNest.ProductService.Models;
using OtakuNest.ProductService.Parameters;

namespace OtakuNest.ProductService.Services;

public class ProductService : IProductService
{
    private readonly ProductDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISortHelper<Product> _sortHelper;
    public ProductService(
        ProductDbContext context,
        IPublishEndpoint publishEndpoint,
        ISortHelper<Product> sortHelper)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _sortHelper = sortHelper;
    }

    public async Task<PagedList<ProductDto>> GetAllAsync(
        ProductParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Name))
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{parameters.Name}%"));

        if (!string.IsNullOrWhiteSpace(parameters.Category))
            query = query.Where(p => p.Category == parameters.Category);

        if (!string.IsNullOrWhiteSpace(parameters.SKU))
            query = query.Where(p => p.SKU == parameters.SKU);

        if (parameters.MinPrice.HasValue)
            query = query.Where(p => p.Price >= parameters.MinPrice.Value);

        if (parameters.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= parameters.MaxPrice.Value);

        if (parameters.IsAvailable.HasValue)
            query = query.Where(p => p.IsAvailable == parameters.IsAvailable.Value);

        if (parameters.MinRating.HasValue)
            query = query.Where(p => p.Rating >= parameters.MinRating.Value);

        if (parameters.MaxRating.HasValue)
            query = query.Where(p => p.Rating <= parameters.MaxRating.Value);

        if (parameters.MinDiscount.HasValue)
            query = query.Where(p => p.Discount >= parameters.MinDiscount.Value);

        if (parameters.MaxDiscount.HasValue)
            query = query.Where(p => p.Discount <= parameters.MaxDiscount.Value);

        query = _sortHelper.ApplySort(query, parameters.OrderBy);

        var paged = await PagedList<Product>.ToPagedListAsync(
            query.AsNoTracking(),
            parameters.PageNumber,
            parameters.PageSize,
            cancellationToken
        );

        var dtoList = paged.Select(MapToDto).ToList();

        return new PagedList<ProductDto>(dtoList, paged.TotalCount, parameters.PageNumber, parameters.PageSize);
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
            product.ImageUrl,
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
        await _publishEndpoint.Publish(new ProductUpdatedEvent(
            product.Id,
            product.Name,
            product.Price,
            product.ImageUrl,
            product.SKU,
            product.Category,
            product.Quantity,
            product.IsAvailable,
            product.Discount,
            product.UpdatedAt), cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product is null) return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync(cancellationToken);
        await _publishEndpoint.Publish(new ProductDeletedEvent(product.Id), cancellationToken);
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
