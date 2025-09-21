using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Entities;
using ProductCatalog.Api.Exceptions;
using ProductCatalog.Api.Mappers;
using ProductCatalog.Api.Persistence;
using ProductCatalog.Api.Responses;
using ProductCatalog.Api.Services.Contracts;

namespace ProductCatalog.Api.Services
{
    public class ProductManagementService : IProductManagementService
    {
        private readonly AppDbContext _db;
        private readonly IProductValidationService _validationService;

        public ProductManagementService(
            AppDbContext dbContext,
            IProductValidationService validationService)
        {
            _db = dbContext;
            _validationService = validationService;
        }

        public async Task<PagedResponse<ProductListItemDto>> GetAllProducts(int pageNumber, int pageSize, CancellationToken ct)
        {
            pageNumber = pageNumber <= 0 ? 1 : pageNumber;
            pageSize = pageSize <= 0 ? 20 : (pageSize > 100 ? 100 : pageSize);

            var query = _db.Products.AsNoTracking();
            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderBy(p => p.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => p.ToListItemDto())
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

        public async Task<ProductDetailsDto> GetProductById(int productId, CancellationToken ct)
        {
            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new ProductNotFoundException(productId);

            return product.ToDetailsDto();
        }

        public async Task<ProductDetailsDto> CreateProduct(ProductCreateDto dto, CancellationToken ct)
        {
            await _validationService.ValidateProductSkuAsync(dto.SKU, cancellationToken: ct);

            var product = dto.ToEntity();
            _db.Products.Add(product);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolationException(ex))
            {
                throw new ProductSkuConflictException(dto.SKU.Trim());
            }

            return product.ToDetailsDto();
        }

        public async Task<ProductDetailsDto> UpdateProduct(int productId, ProductUpdateDto dto, CancellationToken cancellationToken)
        {
            var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
                ?? throw new ProductNotFoundException(productId);

            if (dto.SKU is not null)
            {
                await _validationService.ValidateProductSkuAsync(dto.SKU, productId, cancellationToken);
            }

            ValidateRowVersionFormat(dto.RowVersionBase64);
            SetOriginalRowVersion(entity, dto.RowVersionBase64);
            entity.UpdateEntity(dto);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolationException(ex))
            {
                throw new ProductSkuConflictException(entity.SKU);
            }

            return entity.ToDetailsDto();
        }

        private static void ValidateRowVersionFormat(string rowVersionBase64)
        {
            try
            {
                Convert.FromBase64String(rowVersionBase64);
            }
            catch (FormatException)
            {
                throw new ProductCatalogException("Invalid RowVersion format");
            }
        }

        private void SetOriginalRowVersion(Product entity, string rowVersionBase64)
        {
            _db.Entry(entity).Property(p => p.RowVersion).OriginalValue = Convert.FromBase64String(rowVersionBase64);
        }

        private static bool IsUniqueConstraintViolationException(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException &&
                   (sqlException.Number == SqlErrorCodes.UniqueConstraintViolation ||
                    sqlException.Number == SqlErrorCodes.UniqueIndexViolation);
        }
    }
}
