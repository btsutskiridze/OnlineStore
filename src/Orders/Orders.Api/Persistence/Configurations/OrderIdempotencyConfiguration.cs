using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Api.Entities;

namespace Orders.Api.Persistence.Configurations
{
    public class OrderIdempotencyConfiguration : IEntityTypeConfiguration<OrderIdempotency>
    {
        public void Configure(EntityTypeBuilder<OrderIdempotency> builder)
        {
            builder.ToTable("TB_OrderIdempotencies", "orders");

            builder.HasKey(it => it.Id).IsClustered();

            builder.Property(x => x.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(64)
                .IsUnicode(false);

            builder.HasIndex(it => it.IdempotencyKey).IsUnique();

            builder.Property(x => x.RequestHash)
                .HasMaxLength(64)
                .IsUnicode(false);

            builder.Property(x => x.CreatedAt)
                .HasColumnType("DATETIME")
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .HasColumnType("DATETIME");

            builder.Property(x => x.OrderId);

            builder.Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("TINYINT")
                .IsRequired();
        }
    }
}
