using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly.Registry;
using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Entities;
using ProductCatalog.Api.Enums;
using ProductCatalog.Api.Exceptions;
using ProductCatalog.Api.Persistence;
using ProductCatalog.Api.Resilience;
using ProductCatalog.Api.Responses;
using ProductCatalog.Api.Services.Contracts;

namespace ProductCatalog.Api.Services
{
    public class ProductCatalogService : IProductCatalogService
    {
        private readonly AppDbContext _db;
        private readonly IServiceProvider _serviceProvider;
        private readonly ResiliencePipelineProvider<string> _retryPipelineProvider;
        public ProductCatalogService(AppDbContext db, IServiceProvider serviceProvider, ResiliencePipelineProvider<string> retryPipelineProvider)
        {
            _db = db;
            _serviceProvider = serviceProvider;
            _retryPipelineProvider = retryPipelineProvider;
        }

        public async Task<PagedResponse<ProductListItemDto>> GetAllProductsAsync(int pageNumber, int pageSize, CancellationToken ct)
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : (pageSize > 100 ? 100 : pageSize);

            var query = _db.Products.AsNoTracking();

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderBy(p => p.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductListItemDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    IsActive = p.IsActive
                })
                .ToListAsync(ct);

            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var hasPrev = pageNumber > 1;
            var hasNext = pageNumber < totalPages;

            return new PagedResponse<ProductListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageSize = pageSize,
                PageNumber = pageNumber,
                TotalPages = totalPages,
                HasPrevious = hasPrev,
                HasNext = hasNext
            };
        }

        public async Task<ProductDetailsDto> GetProductByIdAsync(int productId, CancellationToken ct)
        {
            var dto = await _db.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new ProductDetailsDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Price = p.Price,
                    StockQuantity = p.StockQuantity,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    RowVersion = Convert.ToBase64String(p.RowVersion)
                })
                .FirstOrDefaultAsync(ct)
                ?? throw new ProductNotFoundException(productId);
            return dto;
        }

        public async Task<ProductDetailsDto> CreateProductAsync(ProductCreateDto dto, CancellationToken cancellationToken)
        {
            var normalizedSku = dto.SKU.Trim();

            if (await _db.Products.AnyAsync(p => p.SKU == normalizedSku, cancellationToken))
            {
                throw new ProductSkuConflictException(normalizedSku);
            }

            var product = new Product
            {
                Name = dto.Name.Trim(),
                SKU = normalizedSku,
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                IsActive = dto.IsActive
            };

            _db.Products.Add(product);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolationException(ex))
            {
                throw new ProductSkuConflictException(normalizedSku);
            }

            return new ProductDetailsDto
            {
                Id = product.Id,
                Name = product.Name,
                SKU = product.SKU,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                RowVersion = Convert.ToBase64String(product.RowVersion)
            };
        }

        public async Task<ProductDetailsDto> UpdateProductAsync(int productId, ProductUpdateDto dto, CancellationToken ct)
        {
            var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new ProductNotFoundException(productId);

            if (dto.SKU is not null)
            {
                var normalizedSku = dto.SKU.Trim();
                if (normalizedSku != entity.SKU && await _db.Products.AnyAsync(p => p.SKU == normalizedSku && p.Id != productId, ct))
                {
                    throw new ProductSkuConflictException(normalizedSku);
                }
                entity.SKU = normalizedSku;
            }

            try
            {
                var rowVersionBytes = Convert.FromBase64String(dto.RowVersionBase64);
            }
            catch (FormatException)
            {
                throw new ProductCatalogException("Invalid RowVersion format");
            }

            _db.Entry(entity).Property(p => p.RowVersion).OriginalValue = Convert.FromBase64String(dto.RowVersionBase64);
            entity.SKU = dto.SKU?.Trim() ?? entity.SKU;
            entity.StockQuantity = dto.StockQuantity ?? entity.StockQuantity;
            entity.Price = dto.Price ?? entity.Price;
            entity.IsActive = dto.IsActive ?? entity.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolationException(ex))
            {
                throw new ProductSkuConflictException(entity.SKU);
            }

            return new ProductDetailsDto
            {
                Id = entity.Id,
                Name = entity.Name,
                SKU = entity.SKU,
                Price = entity.Price,
                StockQuantity = entity.StockQuantity,
                IsActive = entity.IsActive,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                RowVersion = Convert.ToBase64String(entity.RowVersion)
            };
        }

        public async Task DecrementStockBulkAsync(
            string idempotencyKey,
            IReadOnlyCollection<StockChangeItemDto> items,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ProductCatalogException("Idempotency key is required.");
            if (items is null || items.Count == 0)
                throw new ProductCatalogException("No items to decrement.");
            if (items.Any(i => i.Quantity <= 0))
                throw new ProductCatalogException("Quantities must be positive.");

            var distinctItems = items
                .GroupBy(i => i.ProductId)
                .Select(g => new StockChangeItemDto { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderBy(x => x.ProductId).ToList();

            var retryPipeline = _retryPipelineProvider.GetPipeline(ResiliencePipelines.ProductStockChange);

            await retryPipeline.ExecuteAsync(
                async (context, token) => await DecrementStockInternalAsync(idempotencyKey, distinctItems, token),
            ct);
        }

        private async Task DecrementStockInternalAsync(
            string idempotencyKey,
            List<StockChangeItemDto> items,
            CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            db.InventoryOperations.Add(new InventoryOperation
            {
                IdempotencyKey = idempotencyKey,
                Type = InventoryOperationType.Decrement,
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolationException(ex))
            {
                await transaction.RollbackAsync(ct);
                return;
            }

            foreach (var item in items)
            {
                var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE catalog.TB_Products
                    SET 
                        StockQuantity = StockQuantity - {item.Quantity}, 
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE Id = {item.ProductId} AND StockQuantity >= {item.Quantity} AND IsActive = 1
                    ",
                    ct
                );

                if (affected == 0)
                {
                    await transaction.RollbackAsync(ct);

                    var productExists = await db.Products.AnyAsync(p => p.Id == item.ProductId, ct);
                    var message = productExists
                        ? $"Insufficient stock for product {item.ProductId}"
                        : $"Product {item.ProductId} not found";

                    throw new ProductCatalogException(message);
                }
            }

            await transaction.CommitAsync(ct);
        }

        public async Task ReplenishStocksAsync(
            string idempotencyKey,
            IReadOnlyCollection<StockChangeItemDto> items,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ProductCatalogException("Idempotency key is required.");
            if (items is null || items.Count == 0)
                throw new ProductCatalogException("No items to replenish.");
            if (items.Any(i => i.Quantity <= 0))
                throw new ProductCatalogException("Quantities must be positive.");

            var distinctItems = items
                .GroupBy(i => i.ProductId)
                .Select(g => new StockChangeItemDto { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderBy(x => x.ProductId).ToList();

            var retryPipeline = _retryPipelineProvider.GetPipeline(ResiliencePipelines.ProductStockChange);

            await retryPipeline.ExecuteAsync(
                async (context, token) => await ReplenishStockInternalAsync(idempotencyKey, distinctItems, token),
                ct);
        }

        private async Task ReplenishStockInternalAsync(string idempotencyKey,
            List<StockChangeItemDto> items,
            CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            db.InventoryOperations.Add(new InventoryOperation
            {
                IdempotencyKey = idempotencyKey,
                Type = InventoryOperationType.Increment,
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolationException(ex))
            {
                await transaction.RollbackAsync(ct);
                return;
            }

            foreach (var item in items)
            {
                var affected = await db.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE catalog.TB_Products
                    SET 
                        StockQuantity = StockQuantity + {item.Quantity}, 
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE Id = {item.ProductId}
                    ",
                    ct
                );

                if (affected == 0)
                {
                    await transaction.RollbackAsync(ct);
                    throw new ProductCatalogException($"Product {item.ProductId} not found");
                }
            }

            await transaction.CommitAsync(ct);
        }


        private static bool IsUniqueConstraintViolationException(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627);
        }
    }
}
