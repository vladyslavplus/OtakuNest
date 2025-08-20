using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;
using OtakuNest.Common.Services.Caching;
using OtakuNest.Contracts;
using OtakuNest.ProductService.Data;
using OtakuNest.ProductService.DTOs;
using OtakuNest.ProductService.Models;
using OtakuNest.ProductService.Parameters;

namespace OtakuNest.ProductService.Services;

public class ProductService : IProductService
{
    private const string ProductListKeysSet = "products:list:keys";
    private readonly ProductDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ISortHelper<Product> _sortHelper;
    private readonly IRedisCacheService _cacheService;

    public ProductService(
        ProductDbContext context,
        IPublishEndpoint publishEndpoint,
        ISortHelper<Product> sortHelper,
        IRedisCacheService cacheService)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _sortHelper = sortHelper;
        _cacheService = cacheService;
    }

    public async Task<PagedList<ProductDto>> GetAllAsync(
    ProductParameters parameters,
    CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateListCacheKey(parameters);

        var cachedDto = await _cacheService.GetDataAsync<PagedListCacheDto<ProductDto>>(cacheKey);
        if (cachedDto != null)
        {
            return new PagedList<ProductDto>(
                cachedDto.Items,
                cachedDto.TotalCount,
                cachedDto.PageNumber,
                cachedDto.PageSize
            );
        }

        var query = _context.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Name))
        {
            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                query = query.Where(p => p.Name.Contains(parameters.Name, StringComparison.OrdinalIgnoreCase));
            else
                query = query.Where(p => EF.Functions.ILike(p.Name, $"%{parameters.Name}%"));
        }

        if (!string.IsNullOrWhiteSpace(parameters.Category))
            query = query.Where(p => p.Category == parameters.Category);

        if (!string.IsNullOrWhiteSpace(parameters.SKU))
            query = query.Where(p => p.SKU == parameters.SKU);

        if (!string.IsNullOrWhiteSpace(parameters.Tags))
        {
            if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                query = query.Where(p => p.Tags.Contains(parameters.Tags, StringComparison.OrdinalIgnoreCase));
            else
                query = query.Where(p => EF.Functions.ILike(p.Tags, $"%{parameters.Tags}%"));
        }

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

        var cacheDto = new PagedListCacheDto<ProductDto>
        {
            Items = dtoList,
            TotalCount = paged.TotalCount,
            PageNumber = parameters.PageNumber,
            PageSize = parameters.PageSize
        };

        await _cacheService.SetDataAsync(cacheKey, cacheDto, TimeSpan.FromMinutes(3));
        await _cacheService.AddToSetAsync(ProductListKeysSet, cacheKey);

        return new PagedList<ProductDto>(
            dtoList,
            paged.TotalCount,
            parameters.PageNumber,
            parameters.PageSize
        );
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"product:{id}";

        var cachedProduct = await _cacheService.GetDataAsync<ProductDto>(cacheKey);
        if (cachedProduct != null)
            return cachedProduct;

        var product = await _context.Products.FindAsync(new object[] { id }, cancellationToken);
        if (product == null)
            return null;

        var dto = MapToDto(product);
        await _cacheService.SetDataAsync(cacheKey, dto, TimeSpan.FromMinutes(30));

        return dto;
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

        var productDto = MapToDto(product);

        var cacheKey = $"product:{product.Id}";
        await _cacheService.SetDataAsync(cacheKey, productDto, TimeSpan.FromMinutes(30));

        await InvalidateListCacheAsync();

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

        return productDto;
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

        var cacheKey = $"product:{id}";
        var updatedDto = MapToDto(product);

        await _cacheService.SetDataAsync(cacheKey, updatedDto, TimeSpan.FromMinutes(30));
        await InvalidateListCacheAsync();

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

        var cacheKey = $"product:{id}";
        await _cacheService.RemoveDataAsync(cacheKey);

        await InvalidateListCacheAsync();

        await _publishEndpoint.Publish(new ProductDeletedEvent(product.Id), cancellationToken);
        return true;
    }

    private static string GenerateListCacheKey(ProductParameters parameters)
    {
        return $"products:page:{parameters.PageNumber}:size:{parameters.PageSize}"
               + $":order:{parameters.OrderBy ?? "default"}"
               + $":name:{parameters.Name ?? ""}"
               + $":category:{parameters.Category ?? ""}"
               + $":sku:{parameters.SKU ?? ""}"
               + $":tags:{parameters.Tags ?? ""}" 
               + $":minPrice:{parameters.MinPrice?.ToString() ?? ""}"
               + $":maxPrice:{parameters.MaxPrice?.ToString() ?? ""}"
               + $":isAvailable:{parameters.IsAvailable?.ToString() ?? ""}"
               + $":minRating:{parameters.MinRating?.ToString() ?? ""}"
               + $":maxRating:{parameters.MaxRating?.ToString() ?? ""}"
               + $":minDiscount:{parameters.MinDiscount?.ToString() ?? ""}"
               + $":maxDiscount:{parameters.MaxDiscount?.ToString() ?? ""}";
    }

    private async Task InvalidateListCacheAsync()
    {
        var keys = await _cacheService.GetSetMembersAsync(ProductListKeysSet);
        foreach (var key in keys)
        {
            await _cacheService.RemoveDataAsync(key);
        }
        await _cacheService.ClearSetAsync(ProductListKeysSet);
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