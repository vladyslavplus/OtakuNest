using OtakuNest.ProductService.DTOs;

namespace OtakuNest.ProductService.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ProductDto> CreateAsync(ProductCreateDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(Guid id, ProductUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
