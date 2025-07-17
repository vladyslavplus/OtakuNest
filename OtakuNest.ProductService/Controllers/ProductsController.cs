using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OtakuNest.Common.Extensions;
using OtakuNest.ProductService.DTOs;
using OtakuNest.ProductService.Parameters;
using OtakuNest.ProductService.Services;

namespace OtakuNest.ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll([FromQuery] ProductParameters parameters, CancellationToken cancellationToken)
    {
        var products = await _service.GetAllAsync(parameters, cancellationToken);
        Response.AddPaginationHeader(products);
        return Ok(products);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _service.GetByIdAsync(id, cancellationToken);
        if (product is null)
            return NotFound();
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductDto>> Create(ProductCreateDto dto, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, ProductUpdateDto dto, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(id, dto, cancellationToken);
        if (!updated)
            return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();
        return NoContent();
    }
}
