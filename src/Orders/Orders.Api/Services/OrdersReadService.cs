using Microsoft.EntityFrameworkCore;
using Orders.Api.Dtos;
using Orders.Api.Exceptions;
using Orders.Api.Mappers;
using Orders.Api.Persistence;
using Orders.Api.Services.Contracts;
using System.Security.Claims;

namespace Orders.Api.Services
{
    public class OrdersReadService : IOrdersReadService
    {
        private readonly string _currentUserId;
        private readonly AppDbContext _db;

        public OrdersReadService(IHttpContextAccessor httpContextAccessor, AppDbContext dbContext)
        {
            _currentUserId = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                throw new UnauthorizedAccessException("User is not authenticated.");
            _db = dbContext;
        }

        public async Task<OrderDetailsDto?> GetOrderByIdAsync(Guid guid, CancellationToken ct)
        {
            var order = await _db.Orders
                .AsNoTracking()
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Guid == guid, ct)
                ?? throw new OrderNotFoundException(guid);

            return order.ToOrderDetailsDto();
        }

        public async Task<IReadOnlyList<OrderListItemDto>> GetOrdersByUserIdAsync(CancellationToken ct)
        {
            var orderListItems = await _db.Orders
                .AsNoTracking()
                .Where(x => x.UserId == _currentUserId)
                .Select(x => new OrderListItemDto
                {
                    Guid = x.Guid,
                    TotalAmount = x.TotalAmount,
                    CreatedAt = x.CreatedAt,
                    Status = x.Status.ToString(),
                    ItemCount = x.Items.Count
                })
                .ToListAsync(ct);

            return orderListItems;
        }
    }
}
