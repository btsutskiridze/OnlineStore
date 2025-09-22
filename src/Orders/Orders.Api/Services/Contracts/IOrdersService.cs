using Orders.Api.Dtos;

namespace Orders.Api.Services.Contracts
{
    public interface IOrdersService
    {
        Task<OrderDetailsDto> CreateOrderAsync(
            string idempotencyKey,
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct);

        Task<OrderDetailsDto?> GetOrderByIdAsync(int id, CancellationToken ct);

        Task<IReadOnlyList<OrderListItemDto>> GetOrdersByUserIdAsync(string userId, CancellationToken ct);

        Task<bool> CancelOrderAsync(int id, string userId, CancellationToken ct);
    }
}
