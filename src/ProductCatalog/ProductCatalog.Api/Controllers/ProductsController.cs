using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.Constants;
using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Services.Contracts;

namespace ProductCatalog.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ILogger<ProductsController> _logger;
        private readonly IProductCatalogService _productCatalogService;

        public ProductsController(ILogger<ProductsController> logger, IProductCatalogService productCatalogService)
        {
            _logger = logger;
            _productCatalogService = productCatalogService;
        }

        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.User}")]
        [HttpGet]
        public async Task<ActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default!)
        {
            var products = await _productCatalogService.GetAllProductsAsync(page, pageSize, ct);

            return Ok(products);
        }

        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.User}")]
        [HttpGet("{id:int}")]
        public async Task<ActionResult> Details(int id, CancellationToken ct)
        {
            var product = await _productCatalogService.GetProductByIdAsync(id, ct);

            return Ok(product);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] ProductCreateDto dto, CancellationToken ct)
        {
            var createdProduct = await _productCatalogService.CreateProductAsync(dto, ct);

            return CreatedAtAction(nameof(Details), new { id = createdProduct.Id }, createdProduct);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpPatch("{id:int}")]
        public async Task<ActionResult> Update(int id, [FromBody] ProductUpdateDto dto, CancellationToken ct)
        {
            var updatedProduct = await _productCatalogService.UpdateProductAsync(id, dto, ct);

            return Ok(updatedProduct);
        }

        [Authorize(Policy = "InterServiceAccessOnly")]
        [HttpPost("stock/decrement-bulk")]
        public async Task<ActionResult> Decrement(
            [FromHeader(Name = "Idempotency-Key")] string idempotencykey,
            [FromBody] IReadOnlyList<StockChangeItemDto> items,
            CancellationToken ct)
        {
            await _productCatalogService.DecrementStockBulkAsync(idempotencykey, items, ct);

            return NoContent();
        }

        [Authorize(Policy = "InterServiceAccessOnly")]
        [HttpPost("stock/replenish-bulk")]
        public async Task<ActionResult> Replenish(
            [FromHeader(Name = "Idempotency-Key")] string idempotencykey,
            [FromBody] IReadOnlyList<StockChangeItemDto> items,
            CancellationToken ct)
        {
            await _productCatalogService.ReplenishStocksAsync(idempotencykey, items, ct);

            return NoContent();
        }
    }
}
