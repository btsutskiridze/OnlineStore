using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Api.Dtos;
using Orders.Api.Services.Contracts;

namespace Orders.Api.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = UserRoles.User)]
    public class OrdersController : ControllerBase
    {
        private readonly IOrdersReadService _ordersReadService;
        private readonly IOrdersCreationService _ordersCreationService;
        private readonly IOrdersCancellationService _ordersCancellationService;

        public OrdersController(IOrdersReadService ordersReadService, IOrdersCreationService ordersCreationService, IOrdersCancellationService ordersCancellationService)
        {
            _ordersReadService = ordersReadService;
            _ordersCreationService = ordersCreationService;
            _ordersCancellationService = ordersCancellationService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            [FromBody] IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct)
        {
            var result = await _ordersCreationService.CreateOrderAsync(idempotencyKey, items, ct);

            return CreatedAtAction(nameof(GetOrderById), new { guid = result.Guid }, result);
        }

        [HttpGet("{guid:guid}")]
        public async Task<IActionResult> GetOrderById(Guid guid, CancellationToken ct)
        {
            var result = await _ordersReadService.GetOrderByIdAsync(guid, ct);

            return Ok(result);
        }

        [HttpGet("by-user")]
        public async Task<IActionResult> GetOrdersByUserId(CancellationToken ct)
        {
            var result = await _ordersReadService.GetOrdersByUserIdAsync(ct);

            return Ok(result);
        }

        [HttpPost("{guid:guid}/cancel")]
        public async Task<IActionResult> CancelOrder(Guid guid, CancellationToken ct)
        {
            await _ordersCancellationService.CancelOrderAsync(guid, ct);

            return NoContent();
        }
    }
}
