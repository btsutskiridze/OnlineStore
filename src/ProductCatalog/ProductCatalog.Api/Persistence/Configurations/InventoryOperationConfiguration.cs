using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductCatalog.Api.Entities;

namespace ProductCatalog.Api.Persistence.Configurations
{
    public class InventoryOperationConfiguration : IEntityTypeConfiguration<InventoryOperation>
    {
        public void Configure(EntityTypeBuilder<InventoryOperation> builder)
        {
            builder.ToTable("TB_InventoryOperations", "catalog");

            builder.HasKey(it => it.Id).IsClustered();

            builder.Property(it => it.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(50)
                .IsUnicode(false);
            builder.HasIndex(it => it.IdempotencyKey).IsUnique();

            builder.Property(it => it.Type)
                .HasConversion<byte>()
                .HasColumnType("TINYINT")
                .IsRequired();

            builder.Property(it => it.CreatedAt)
                .IsRequired()
                .HasColumnType("DATETIME")
                .HasDefaultValueSql("SYSUTCDATETIME()");
        }
    }
}
