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
            var order = _db.Orders
                .AsNoTracking()
                .Include(x => x.Items)
                .FirstOrDefault(x => x.Guid == guid)
                ?? throw new OrderNotFoundException(guid);

            return order.ToOrderDetailsDto();
        }

        public async Task<IReadOnlyList<OrderListItemDto>> GetOrdersByUserIdAsync(CancellationToken ct)
        {
            var orderListItems = await _db.Orders
                .AsNoTracking()
                .Where(x => x.UserId == _currentUserId)
                .Select(x => x.ToOrderListItemDto())
                .ToListAsync(ct);

            return orderListItems;
        }
    }
}
