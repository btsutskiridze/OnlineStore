using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Api.Entities;

namespace Orders.Api.Persistence.Configurations
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("TB_OrderItems", "orders");

            builder.HasKey(oi => oi.Id).IsClustered();

            builder.Property(oi => oi.OrderId)
                .IsRequired();

            builder.Property(oi => oi.ProductId)
                .IsRequired();

            builder.Property(oi => oi.Quantity)
                .IsRequired();

            builder.Property(oi => oi.UnitPrice)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(oi => oi.LineTotal)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(oi => oi.ProductName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(oi => oi.SKU)
                .IsRequired()
                .HasMaxLength(20)
                .IsUnicode(false);

            builder.HasIndex(oi => new { oi.OrderId, oi.ProductId }).IsUnique();
        }
    }
}
