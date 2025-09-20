using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductCatalog.Api.Entities;

namespace ProductCatalog.Api.EntityConfigs.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("TB_Product", "catalog");

            builder.HasKey(p => p.Id).IsClustered();

            builder.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(p => p.SKU)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false);
            builder.HasIndex(p => p.SKU).IsUnique();

            builder.Property(p => p.Price)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(p => p.StockQuantity)
                .IsRequired();

            builder.Property(p => p.IsActive)
                .IsRequired();

            builder.Property(p => p.CreatedAt)
                .IsRequired()
                .HasColumnType("DATETIME")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.Property(p => p.UpdatedAt)
                .HasColumnType("DATETIME");

            builder.Property(p => p.RowVersion)
                .IsRowVersion();
        }
    }
}
