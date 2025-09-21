using ProductCatalog.Api.Dtos;

namespace ProductCatalog.Api.Services.Contracts
{
    public interface IProductStockService
    {
        Task DecrementStockBatch(
            string idempotencyKey,
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken cancellationToken = default);

        Task ReplenishStockBatch(
            string idempotencyKey,
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken cancellationToken = default);
    }
}
