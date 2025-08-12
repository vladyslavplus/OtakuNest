using Microsoft.AspNetCore.Mvc;
using OtakuNest.SearchService.DTOs;
using OtakuNest.SearchService.Services;

namespace OtakuNest.SearchService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly IElasticService _elasticService;

        public SearchController(IElasticService elasticService)
        {
            _elasticService = elasticService;
        }

        [HttpGet("products")]
        public async Task<ActionResult<IEnumerable<ProductElasticDto>>> SearchProducts(
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int size = 20,
            CancellationToken cancellationToken = default)
        {
            var products = await _elasticService.SearchByNameAsync(query, page, size, cancellationToken);
            return Ok(products);
        }

        [HttpGet("health")]
        public async Task<ActionResult> GetHealth(CancellationToken cancellationToken = default)
        {
            var isHealthy = await _elasticService.IsHealthyAsync(cancellationToken);

            if (isHealthy)
                return Ok(new { status = "healthy" });

            return StatusCode(503, new { status = "unhealthy" });
        }
    }
}
