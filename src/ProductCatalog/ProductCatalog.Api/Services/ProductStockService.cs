using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Registry;
using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Entities;
using ProductCatalog.Api.Enums;
using ProductCatalog.Api.Exceptions;
using ProductCatalog.Api.Persistence;
using ProductCatalog.Api.Services.Contracts;

namespace ProductCatalog.Api.Services
{
    public class ProductStockService : IProductStockService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ResiliencePipeline _retryPipeline;
        private readonly IProductValidationService _validationService;

        public ProductStockService(
            IServiceProvider serviceProvider,
            ResiliencePipelineProvider<string> retryPipelineProvider,
            IProductValidationService validationService)
        {
            _serviceProvider = serviceProvider;
            _retryPipeline = retryPipelineProvider.GetPipeline(ResiliencePipelines.DatabaseOperations);
            _validationService = validationService;
        }

        public async Task DecrementStockBatch(
            string idempotencyKey,
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct = default)
        {
            _validationService.ValidateIdempotencyKey(idempotencyKey);
            _validationService.ValidateProductQuantities(items);

            var distinctItems = GroupAndSortItems(items);

            await _retryPipeline.ExecuteAsync(
                async (_, token) => await ExecuteStockDecrementAsync(idempotencyKey, distinctItems, token),
                ct);
        }

        public async Task ReplenishStockBatch(
            string idempotencyKey,
            IReadOnlyList<ProductQuantityItemDto> items,
            CancellationToken ct = default)
        {
            _validationService.ValidateIdempotencyKey(idempotencyKey);
            _validationService.ValidateProductQuantities(items);

            var distinctItems = GroupAndSortItems(items);

            await _retryPipeline.ExecuteAsync(
                async (_, token) => await ExecuteStockReplenishmentAsync(idempotencyKey, distinctItems, token),
                ct);
        }

        private static List<ProductQuantityItemDto> GroupAndSortItems(IReadOnlyList<ProductQuantityItemDto> items)
        {
            return items
                .GroupBy(item => item.ProductId)
                .Select(g => new ProductQuantityItemDto { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderBy(x => x.ProductId)
                .ToList();
        }

        private async Task ExecuteStockDecrementAsync(
            string idempotencyKey,
            List<ProductQuantityItemDto> items,
            CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            try
            {
                await CreateInventoryOperationAsync(db, idempotencyKey, InventoryOperationType.Decrement, ct);
                await ProcessStockDecrementsAsync(db, items, ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (IsIdempotencyKeyConflict(ex))
            {
                await transaction.RollbackAsync(ct);
                return;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        private async Task ExecuteStockReplenishmentAsync(
            string idempotencyKey,
            List<ProductQuantityItemDto> items,
            CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            try
            {
                await CreateInventoryOperationAsync(db, idempotencyKey, InventoryOperationType.Increment, ct);
                await ProcessStockReplenishmentsAsync(db, items, ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException ex) when (IsIdempotencyKeyConflict(ex))
            {
                await transaction.RollbackAsync(ct);
                // Idempotent case - operation already processed
                return;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        private static async Task CreateInventoryOperationAsync(
            AppDbContext db,
            string idempotencyKey,
            InventoryOperationType operationType,
            CancellationToken ct)
        {
            db.InventoryOperations.Add(new InventoryOperation
            {
                IdempotencyKey = idempotencyKey,
                Type = operationType,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(ct);
        }

        private async Task ProcessStockDecrementsAsync(
            AppDbContext db,
            List<ProductQuantityItemDto> items,
            CancellationToken ct)
        {
            foreach (var item in items)
            {
                var affectedRows = await ExecuteStockDecrementSqlAsync(db, item, ct);

                if (affectedRows == 0)
                {
                    var errorMessage = await GetStockDecrementErrorMessageAsync(db, item.ProductId, ct);
                    throw new ProductCatalogException(errorMessage);
                }
            }
        }

        private async Task ProcessStockReplenishmentsAsync(
            AppDbContext db,
            List<ProductQuantityItemDto> items,
            CancellationToken ct)
        {
            foreach (var item in items)
            {
                var affectedRows = await ExecuteStockReplenishmentSqlAsync(db, item, ct);

                if (affectedRows == 0)
                {
                    throw new ProductCatalogException($"Product {item.ProductId} not found");
                }
            }
        }

        private static async Task<int> ExecuteStockDecrementSqlAsync(
            AppDbContext dbContext,
            ProductQuantityItemDto item,
            CancellationToken ct)
        {
            return await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE catalog.TB_Products
                SET 
                    StockQuantity = StockQuantity - {item.Quantity}, 
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = {item.ProductId} 
                  AND StockQuantity >= {item.Quantity} 
                  AND IsActive = 1",
                ct);
        }

        private static async Task<int> ExecuteStockReplenishmentSqlAsync(
            AppDbContext dbContext,
            ProductQuantityItemDto item,
            CancellationToken ct)
        {
            return await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE catalog.TB_Products
                SET 
                    StockQuantity = StockQuantity + {item.Quantity}, 
                    UpdatedAt = SYSUTCDATETIME()
                WHERE Id = {item.ProductId}",
                ct);
        }

        private static async Task<string> GetStockDecrementErrorMessageAsync(
            AppDbContext dbContext,
            int productId,
            CancellationToken ct)
        {
            var productExists = await dbContext.Products.AnyAsync(p => p.Id == productId, ct);

            return productExists
                ? $"Not enough stock for product {productId}"
                : $"Product {productId} doesn't exist";
        }

        private static bool IsIdempotencyKeyConflict(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException &&
                   (sqlException.Number == SqlErrorCodes.UniqueConstraintViolation ||
                    sqlException.Number == SqlErrorCodes.UniqueIndexViolation);
        }
    }
}
