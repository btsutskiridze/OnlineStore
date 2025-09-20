using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Responses;

namespace ProductCatalog.Api.Services.Contracts
{
    public interface IProductCatalogService
    {
        Task<PagedResponse<ProductListItemDto>> GetAllProducts(int pageNumber, int pageSize, CancellationToken ct);
        Task<ProductDetailsDto> GetProductById(int productId, CancellationToken ct);
        Task<ProductDetailsDto> CreateProduct(ProductCreateDto newProduct, CancellationToken ct);
        Task<ProductDetailsDto> UpdateProduct(int productId, ProductUpdateDto updatedProduct, CancellationToken ct);
        Task DecrementStockBatch(string IdempotencyKey, IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct);
        Task ReplenishStockBatch(string IdempotencyKey, IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct);
        Task<IReadOnlyList<ProductValidationResultDto>> ValidateProducts(IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct);
    }
}
