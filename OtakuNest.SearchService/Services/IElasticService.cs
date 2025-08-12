using OtakuNest.SearchService.DTOs;

namespace OtakuNest.SearchService.Services
{
    public interface IElasticService
    {
        Task IndexProductAsync(ProductElasticDto product, CancellationToken cancellationToken = default);
        Task UpdateProductAsync(ProductElasticDto product, CancellationToken cancellationToken = default);
        Task DeleteProductAsync(Guid productId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ProductElasticDto>> SearchByNameAsync(string productName, int page = 1, int size = 20, CancellationToken cancellationToken = default);
        Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    }
}
