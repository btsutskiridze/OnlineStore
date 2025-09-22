using Orders.Api.Dtos;

namespace Orders.Api.Services.Contracts
{
    public interface IOrdersCreationService
    {
        Task<OrderDetailsDto> CreateOrderAsync(
            string idempotencyKey,
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct);
    }
}
