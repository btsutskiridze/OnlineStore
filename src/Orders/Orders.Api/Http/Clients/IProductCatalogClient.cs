using Orders.Api.Dtos;

namespace Orders.Api.Http.Clients
{
    public interface IProductCatalogClient
    {
        Task<List<ProductValidationResultDto>> ValidateAsync(
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct);

        Task DecrementStockAsync(
            string idempotencyKey,
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct);

        Task ReplenishStockAsync(
            string idempotencyKey,
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct);
    }
}
