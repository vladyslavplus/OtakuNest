using OtakuNest.Common.Helpers;
using OtakuNest.ProductService.DTOs;
using OtakuNest.ProductService.Parameters;

namespace OtakuNest.ProductService.Services
{
    public interface IProductService
    {
        Task<PagedList<ProductDto>> GetAllAsync(ProductParameters parameters, CancellationToken cancellationToken = default);
        Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<ProductDto> CreateAsync(ProductCreateDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(Guid id, ProductUpdateDto dto, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
