using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Services.Contracts;

namespace ProductCatalog.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductManagementService _productManagementService;
        private readonly IProductStockService _productStockService;
        private readonly IProductValidationService _productValidationService;
        public ProductsController(
            IProductManagementService productManagementService,
            IProductStockService productStockService,
            IProductValidationService productValidationService)
        {
            _productManagementService = productManagementService;
            _productStockService = productStockService;
            _productValidationService = productValidationService;
        }

        [Authorize(Roles = UserRoles.AdminOrUser)]
        [HttpGet]
        public async Task<ActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default!)
        {
            var products = await _productManagementService.GetAllProducts(page, pageSize, cancellationToken);

            return Ok(products);
        }

        [Authorize(Roles = UserRoles.AdminOrUser)]
        [HttpGet("{id:int}")]
        public async Task<ActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var product = await _productManagementService.GetProductById(id, cancellationToken);

            return Ok(product);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpPost]
        public async Task<ActionResult> Create([FromBody] ProductCreateDto dto, CancellationToken cancellationToken)
        {
            var createdProduct = await _productManagementService.CreateProduct(dto, cancellationToken);

            return CreatedAtAction(nameof(Details), new { id = createdProduct.Id }, createdProduct);
        }

        [Authorize(Roles = UserRoles.Admin)]
        [HttpPatch("{id:int}")]
        public async Task<ActionResult> Update(int id, [FromBody] ProductUpdateDto dto, CancellationToken cancellationToken)
        {
            var updatedProduct = await _productManagementService.UpdateProduct(id, dto, cancellationToken);

            return Ok(updatedProduct);
        }

        [Authorize(Policy = Policies.InterServiceAccessOnly)]
        [HttpPost("validate")]
        public async Task<ActionResult> Validate([FromBody] IReadOnlyList<ProductQuantityItemDto> items, CancellationToken cancellationToken)
        {
            var validationResults = await _productValidationService.ValidateProducts(items, cancellationToken);

            return Ok(validationResults);
        }

        [Authorize(Policy = Policies.InterServiceAccessOnly)]
        [HttpPost("stock/decrement-batch")]
        public async Task<ActionResult> Decrement(
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            [FromBody] IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken cancellationToken)
        {
            await _productStockService.DecrementStockBatch(idempotencyKey, items, cancellationToken);

            return NoContent();
        }

        [Authorize(Policy = Policies.InterServiceAccessOnly)]
        [HttpPost("stock/replenish-batch")]
        public async Task<ActionResult> Replenish(
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            [FromBody] IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken cancellationToken)
        {
            await _productStockService.ReplenishStockBatch(idempotencyKey, items, cancellationToken);

            return NoContent();
        }
    }
}
