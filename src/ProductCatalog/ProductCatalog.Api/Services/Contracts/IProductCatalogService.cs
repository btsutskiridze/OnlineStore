using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Responses;

namespace ProductCatalog.Api.Services.Contracts
{
    public interface IProductCatalogService
    {
        Task<PagedResponse<ProductListItemDto>> GetAllProductsAsync(int pageNumber, int pageSize, CancellationToken ct);
        Task<ProductDetailsDto> GetProductByIdAsync(int productId, CancellationToken ct);
        Task<ProductDetailsDto> CreateProductAsync(ProductCreateDto newProduct, CancellationToken ct);
        Task<ProductDetailsDto> UpdateProductAsync(int productId, ProductUpdateDto updatedProduct, CancellationToken ct);
        Task DecrementStockBulkAsync(string IdempotencyKey, IReadOnlyCollection<StockChangeItemDto> items, CancellationToken ct);
        Task ReplenishStocksAsync(string IdempotencyKey, IReadOnlyCollection<StockChangeItemDto> items, CancellationToken ct);
    }
}
