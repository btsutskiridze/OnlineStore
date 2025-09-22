using Orders.Api.Dtos;

namespace Orders.Api.Services.Contracts
{
    public interface IOrdersReadService
    {
        Task<OrderDetailsDto?> GetOrderByIdAsync(Guid guid, CancellationToken ct);

        Task<IReadOnlyList<OrderListItemDto>> GetOrdersByUserIdAsync(CancellationToken ct);
    }
}
