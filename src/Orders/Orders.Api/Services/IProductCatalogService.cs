namespace Orders.Api.Services
{
    public interface IProductCatalogService
    {
        Task<bool> DecrementStockAsync(int productId, int quantity, CancellationToken cancellationToken = default);
        Task<bool> IsProductAvailableAsync(int productId, int quantity, CancellationToken cancellationToken = default);
    }
}
