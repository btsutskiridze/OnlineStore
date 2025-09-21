using ProductCatalog.Api.Dtos;

namespace ProductCatalog.Api.Services.Contracts
{
    public interface IProductValidationService
    {
        Task<List<ProductValidationResultDto>> ValidateProducts(
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken cancellationToken = default);

        void ValidateIdempotencyKey(string idempotencyKey);
        void ValidateProductQuantities(IReadOnlyList<ProductQuantityItemDto> items);
        Task ValidateProductSkuAsync(string sku, int? excludeProductId = null, CancellationToken cancellationToken = default);
    }
}
