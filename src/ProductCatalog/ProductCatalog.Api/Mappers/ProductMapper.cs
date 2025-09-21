using ProductCatalog.Api.Dtos;
using ProductCatalog.Api.Entities;

namespace ProductCatalog.Api.Mappers
{
    public static class ProductMapper
    {
        public static ProductDetailsDto ToDetailsDto(this Product product)
        {
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

        public static ProductListItemDto ToListItemDto(this Product product)
        {
            return new ProductListItemDto
            {
                Id = product.Id,
                Name = product.Name,
                SKU = product.SKU,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                IsActive = product.IsActive
            };
        }

        public static Product ToEntity(this ProductCreateDto dto)
        {
            return new Product
            {
                Name = dto.Name.Trim(),
                SKU = dto.SKU.Trim(),
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                IsActive = dto.IsActive
            };
        }

        public static void UpdateEntity(this Product entity, ProductUpdateDto dto)
        {
            if (dto.SKU is not null)
                entity.SKU = dto.SKU.Trim();

            if (dto.StockQuantity.HasValue)
                entity.StockQuantity = dto.StockQuantity.Value;

            if (dto.Price.HasValue)
                entity.Price = dto.Price.Value;

            if (dto.IsActive.HasValue)
                entity.IsActive = dto.IsActive.Value;

            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
