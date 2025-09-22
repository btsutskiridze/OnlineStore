using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orders.Api.Dtos;
using Orders.Api.Services.Contracts;
using System.Security.Claims;

namespace Orders.Api.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrdersService _ordersService;
        public OrdersController(IOrdersService ordersService)
        {
            _ordersService = ordersService;
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.User)]
        public async Task<IActionResult> CreateOrder(
            [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
            [FromBody] IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct)
        {
            var result = await _ordersService.CreateOrderAsync(
                idempotencyKey,
                items,
                ct);

            return CreatedAtAction(nameof(GetOrderById), new { id = result.Guid }, result);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = UserRoles.User)]
        public async Task<IActionResult> GetOrderById(int id, CancellationToken ct)
        {
            var result = await _ordersService.GetOrderByIdAsync(id, ct);
            if (result is null) return NotFound();

            return Ok(result);
        }

        [HttpGet("user/{userId}")]
        [Authorize(Roles = UserRoles.User)]
        public async Task<IActionResult> GetOrdersByUserId(string userId, CancellationToken ct)
        {
            var result = await _ordersService.GetOrdersByUserIdAsync(userId, ct);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = UserRoles.User)]
        public async Task<IActionResult> CancelOrder(int id, CancellationToken ct)
        {
            var result = await _ordersService.CancelOrderAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), ct);
            if (!result) return NotFound();

            return NoContent();
        }
    }
}
