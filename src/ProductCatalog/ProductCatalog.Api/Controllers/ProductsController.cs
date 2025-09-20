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
            var products = await _productCatalogService.GetAllProducts(page, pageSize, ct);

            return Ok(products);
        }

        [Authorize(Roles = $"{UserRoles.Admin},{UserRoles.User}")]
        [HttpGet("{id:int}")]
        public async Task<ActionResult> Details(int id, CancellationToken ct)
        {
            var product = await _productCatalogService.GetProductById(id, ct);

            return Ok(product);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] ProductCreateDto dto, CancellationToken ct)
        {
            var createdProduct = await _productCatalogService.CreateProduct(dto, ct);

            return CreatedAtAction(nameof(Details), new { id = createdProduct.Id }, createdProduct);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpPatch("{id:int}")]
        public async Task<ActionResult> Update(int id, [FromBody] ProductUpdateDto dto, CancellationToken ct)
        {
            var updatedProduct = await _productCatalogService.UpdateProduct(id, dto, ct);

            return Ok(updatedProduct);
        }

        [Authorize(Policy = "InterServiceAccessOnly")]
        [HttpPost("validate")]
        public async Task<ActionResult> Validate([FromBody] IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct)
        {
            await _productCatalogService.ValidateProducts(items, ct);

            return NoContent();
        }

        [Authorize(Policy = "InterServiceAccessOnly")]
        [HttpPost("stock/decrement-batch")]
        public async Task<ActionResult> Decrement(
            [FromHeader(Name = "Idempotency-Key")] string idempotencykey,
            [FromBody] IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct)
        {
            await _productCatalogService.DecrementStockBatch(idempotencykey, items, ct);

            return NoContent();
        }

        [Authorize(Policy = "InterServiceAccessOnly")]
        [HttpPost("stock/replenish-batch")]
        public async Task<ActionResult> Replenish(
            [FromHeader(Name = "Idempotency-Key")] string idempotencykey,
            [FromBody] IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct)
        {
            await _productCatalogService.ReplenishStockBatch(idempotencykey, items, ct);

            return NoContent();
        }
    }
}
