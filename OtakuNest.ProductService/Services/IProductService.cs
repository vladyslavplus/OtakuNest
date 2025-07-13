using OtakuNest.ProductService.DTOs;

namespace OtakuNest.ProductService.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDto>> GetAllAsync();
        Task<ProductDto?> GetByIdAsync(Guid id);
        Task<ProductDto> CreateAsync(ProductCreateDto dto);
        Task<bool> UpdateAsync(Guid id, ProductUpdateDto dto);
        Task<bool> DeleteAsync(Guid id);
    }
}
