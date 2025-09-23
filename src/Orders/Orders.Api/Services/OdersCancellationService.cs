using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Entities;
using Orders.Api.Enums;
using Orders.Api.Exceptions;
using Orders.Api.Http.Clients;
using Orders.Api.Mappers;
using Orders.Api.Persistence;
using Orders.Api.Services.Contracts;

namespace Orders.Api.Services
{
    public class OdersCancellationService : IOrdersCancellationService
    {

        private readonly string _currentUserId;
        private readonly IProductCatalogClient _catalogClient;
        private readonly AppDbContext _db;

        public OdersCancellationService(IHttpContextAccessor httpContextAccessor, IProductCatalogClient productCatalogClient, AppDbContext dbContext)
        {
            _currentUserId = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                throw new UnauthorizedAccessException("User is not authenticated.");
            _catalogClient = productCatalogClient;
            _db = dbContext;
        }

        public async Task CancelOrderAsync(Guid guid, string rowVersionBase64, CancellationToken ct)
        {
            ValidateRowVersionFormat(rowVersionBase64);

            var order = await _db.Orders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Guid == guid && x.UserId == _currentUserId, ct)
                ?? throw new OrderNotFoundException(guid);

            if (order.Status == OrderStatus.Cancelled)
                return;

            if (order.Status != OrderStatus.Pending)
                throw new OrdersException("Order is not pending");

            try
            {
                await ReplenishStockForCancellation(order, ct);
            }
            catch
            {
                throw new OrdersException("Failed to replenish stock for order cancellation");
            }

            try
            {
                await CancelOrder(order, rowVersionBase64, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new OrdersException("Concurrency conflict occurred while cancelling order");
            }
            catch
            {
                throw new OrdersException("Failed to cancel order");
            }
        }

        private static void ValidateRowVersionFormat(string rowVersionBase64)
        {
            try
            {
                Convert.FromBase64String(rowVersionBase64);
            }
            catch
            {
                throw new OrdersException("Invalid RowVersion format");
            }
        }

        private void SetOriginalRowVersion(Order order, string rowVersionBase64)
        {
            _db.Entry(order).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(rowVersionBase64);
        }

        private async Task ReplenishStockForCancellation(Order order, CancellationToken ct)
        {
            var cancelKey = $"cancel:{order.Guid}";

            await _catalogClient.ReplenishStockAsync(
                cancelKey,
                order.Items.Select(x => x.ToProductQuantityItemDto()).ToList(),
                ct);
        }

        private async Task CancelOrder(Order order, string rowVersionBase64, CancellationToken ct)
        {
            SetOriginalRowVersion(order, rowVersionBase64);
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            order.CancelledAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
