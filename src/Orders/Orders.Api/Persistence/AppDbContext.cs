using Microsoft.EntityFrameworkCore;
using Orders.Api.Entities;

namespace Orders.Api.Persistence
{
    public class AppDbContext : DbContext
    {
        public DbSet<Order> Orders;
        public DbSet<OrderItem> OrderItems;
        public DbSet<OrderIdempotency> OrderIdempotencies;
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
