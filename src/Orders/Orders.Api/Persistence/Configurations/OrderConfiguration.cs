using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Api.Entities;

namespace Orders.Api.Persistence.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("TB_Orders", "orders");
            builder.HasKey(o => o.Id).IsClustered();

            builder.HasIndex(o => o.Guid).IsUnique();

            builder.Property(o => o.UserId)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(o => o.Status)
                .IsRequired();

            builder.Property(o => o.TotalAmount)
                .IsRequired()
                .HasPrecision(18, 2);

            builder.Property(o => o.CreatedAt)
                .IsRequired()
                .HasColumnType("DATETIME")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            builder.Property(o => o.UpdatedAt)
                .HasColumnType("DATETIME");

            builder.Property(o => o.CancelledAt)
                .HasColumnType("DATETIME");

            builder.Property(o => o.RowVersion)
                .IsRowVersion();

            builder.HasMany(o => o.Items)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId);
        }
    }
}
