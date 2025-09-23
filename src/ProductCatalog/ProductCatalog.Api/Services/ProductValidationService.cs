using Microsoft.EntityFrameworkCore;
using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Entities;
using ProductCatalog.Api.Exceptions;
using ProductCatalog.Api.Persistence;
using ProductCatalog.Api.Services.Contracts;

namespace ProductCatalog.Api.Services
{
    public class ProductValidationService : IProductValidationService
    {
        private readonly AppDbContext _db;

        public ProductValidationService(AppDbContext dbContext)
        {
            _db = dbContext;
        }

        public async Task<List<ProductValidationResultDto>> ValidateProducts(
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct = default)
        {
            ValidateProductQuantities(items);

            var distinctItems = items
                .GroupBy(item => item.ProductId)
                .Select(g => new ProductQuantityItemDto { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();

            var productIds = distinctItems
                .Select(d => d.ProductId)
                .ToList();

            var productSnapshot = await _db.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            return BuildValidationResults(distinctItems, productSnapshot);
        }

        public void ValidateIdempotencyKey(string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ProductCatalogException("Idempotency key is required.");
        }

        public void ValidateProductQuantities(IReadOnlyList<ProductQuantityItemDto> items)
        {
            if (items is null || items.Count == 0)
                throw new ProductCatalogException("No items provided.");

            if (items.Any(item => item.Quantity <= 0))
                throw new ProductCatalogException("All quantities must be positive.");
        }

        public async Task ValidateProductSkuAsync(string sku, int? excludeProductId = null, CancellationToken ct = default)
        {
            var normalizedSku = sku.Trim();

            var query = _db.Products.Where(p => p.SKU == normalizedSku);

            if (excludeProductId.HasValue)
            {
                query = query.Where(p => p.Id != excludeProductId.Value);
            }

            if (await query.AnyAsync(ct))
            {
                throw new ProductSkuConflictException(normalizedSku);
            }
        }

        private static List<ProductValidationResultDto> BuildValidationResults(
            List<ProductQuantityItemDto> distinctItems,
            Dictionary<int, Product> productSnapshot)
        {
            var results = new List<ProductValidationResultDto>(distinctItems.Count);

            foreach (var item in distinctItems)
            {
                if (!productSnapshot.TryGetValue(item.ProductId, out var product))
                {
                    results.Add(new ProductValidationResultDto
                    {
                        ProductId = item.ProductId,
                        RequestedQuantity = item.Quantity,
                        CanFulfill = false,
                        Exists = false
                    });

                    continue;
                }
                bool canFulfill = product.IsActive && product.StockQuantity >= item.Quantity;

                results.Add(new ProductValidationResultDto
                {
                    ProductId = product.Id,
                    RequestedQuantity = item.Quantity,
                    CanFulfill = canFulfill,
                    Exists = true,
                    Name = product.Name,
                    Sku = product.SKU,
                    Price = product.Price
                });
            }

            return results;
        }
    }
}
