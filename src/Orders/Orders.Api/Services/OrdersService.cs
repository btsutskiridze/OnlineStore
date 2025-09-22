using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Orders.Api.Dtos;
using Orders.Api.Entities;
using Orders.Api.Enums;
using Orders.Api.Exceptions;
using Orders.Api.Helpers;
using Orders.Api.Http.Clients;
using Orders.Api.Mappers;
using Orders.Api.Persistence;
using Orders.Api.Services.Contracts;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Orders.Api.Services
{
    public class OrdersService : IOrdersService
    {
        private readonly string _userId;
        private readonly IProductCatalogClient _productCatalogClient;
        private readonly AppDbContext _db;

        public OrdersService(IHttpContextAccessor httpContextAccessor, IProductCatalogClient productCatalogClient, AppDbContext dbContext)
        {
            _userId = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                throw new UnauthorizedAccessException("User is not authenticated.");
            _productCatalogClient = productCatalogClient;
            _db = dbContext;
        }

        public async Task<OrderDetailsDto> CreateOrderAsync(string idempotencyKey, IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct)
        {
            ValidateIdempotencyKey(idempotencyKey);
            ValidateProductQuantities(items);

            var productItems = GetGroupedProductItems(items);

            var requestHash = BuildDeterministicKey(productItems);

            var idem = await _db.OrderIdempotencies
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

            if (idem is not null)
            {
                if (idem.RequestHash is not null && !string.Equals(idem.RequestHash, requestHash, StringComparison.Ordinal))
                    throw new OrdersException("Idempotency key has already been used with different request parameters.");

                switch (idem.Status)
                {
                    case IdempotencyStatus.Completed:
                        if (string.IsNullOrEmpty(idem.ResponseBody) || idem.ResponseCode != (int)HttpStatusCode.Created)
                            throw new OrdersException("Idempotent replay has no stored response.");

                        return JsonSerializer.Deserialize<OrderDetailsDto>(idem.ResponseBody)!;
                    case IdempotencyStatus.Failed:
                        throw new OrdersException("The previous operation with the same idempotency key failed.");
                }

                if (idem.Status == IdempotencyStatus.Started)
                {
                    int InFlightWindowSeconds = 45;
                    var age = DateTime.UtcNow - (idem.UpdatedAt ?? idem.CreatedAt);
                    if (age < TimeSpan.FromSeconds(InFlightWindowSeconds))
                    {
                        throw new OrdersException("Request with this Idempotency-Key is in progress. Please retry.");
                    }

                    idem.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                }
            }
            else
            {
                try
                {
                    idem = new()
                    {
                        IdempotencyKey = idempotencyKey,
                        RequestHash = requestHash,
                        Status = IdempotencyStatus.Started,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.OrderIdempotencies.Add(idem);
                    await _db.SaveChangesAsync(ct);

                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolationException(ex))
                {
                    throw new OrdersException("An operation with the same idempotency key is already in progress. Please try again later.");
                }
            }

            var validatedProductQuantities = await _productCatalogClient.ValidateAsync(productItems, ct);

            EnsureAllAvailable(validatedProductQuantities);
            EnsureSameSet(productItems, validatedProductQuantities);

            Order order;

            await using (var tx = await _db.Database.BeginTransactionAsync(ct))
            {
                try
                {
                    order = BuildPendingOrder(validatedProductQuantities);
                    _db.Orders.Add(order);
                    await _db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                }
                catch
                {
                    await tx.RollbackAsync(ct);
                    throw new OrdersException("Failed to create order");
                }
            }

            try
            {
                await _productCatalogClient.DecrementStockAsync(idempotencyKey, productItems, ct);
            }
            catch
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                var persisted = await _db.Orders.SingleAsync(o => o.Id == order.Id, ct);
                persisted.Status = OrderStatus.Rejected;
                persisted.UpdatedAt = DateTime.UtcNow;

                var idemRow = await _db.OrderIdempotencies
                    .SingleAsync(i => i.IdempotencyKey == idempotencyKey, ct);
                idemRow.Status = IdempotencyStatus.Failed;
                idemRow.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                throw new OrdersException("Stock reservation rejected");
            }

            var orderDetailsDto = order.ToOrderDetailsDto();

            await using (var tx = await _db.Database.BeginTransactionAsync(ct))
            {
                var idemRow = await _db.OrderIdempotencies
                    .SingleAsync(i => i.IdempotencyKey == idempotencyKey, ct);

                idemRow.Status = IdempotencyStatus.Completed;
                idemRow.OrderId = order.Id;
                idemRow.ResponseBody = JsonSerializer.Serialize(orderDetailsDto);
                idemRow.ResponseCode = (int)HttpStatusCode.Created;
                idemRow.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }

            return orderDetailsDto;
        }

        private Order BuildPendingOrder(List<ProductValidationResultDto> validatedProductQuantities)
        {
            var order = new Order
            {
                Guid = Guid.NewGuid(),
                UserId = _userId,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Items = validatedProductQuantities.Select(pi => new OrderItem
                {
                    ProductId = pi.ProductId,
                    ProductName = pi.Name!,
                    UnitPrice = (decimal)pi.Price!,
                    Quantity = pi.RequestedQuantity,
                    SKU = pi.Sku!,
                    LineTotal = (decimal)pi.Price! * pi.RequestedQuantity,
                }).ToList()
            };

            order.TotalAmount = order.Items.Sum(i => i.LineTotal);

            return order;
        }

        private static string BuildDeterministicKey(IReadOnlyList<ProductQuantityItemDto> items)
        {
            var sb = new StringBuilder();
            foreach (var it in items)
                sb.Append(it.ProductId).Append(':').Append(it.Quantity).Append(';');
            return HashUtils.ComputeSha256Hash(sb.ToString());
        }


        private static void ValidateIdempotencyKey(string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new OrdersException("Idempotency key is required.");
        }

        private static List<ProductQuantityItemDto> GetGroupedProductItems(IReadOnlyList<ProductQuantityItemDto> items)
        {
            return items
                .GroupBy(i => i.ProductId)
                .Select(g => new ProductQuantityItemDto { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderBy(x => x.ProductId)
                .ToList();
        }

        private static void EnsureAllAvailable(IEnumerable<ProductValidationResultDto> validation)
        {
            var bad = validation.FirstOrDefault(v => !v.CanFulfill);
            if (bad is not null)
                throw new OrdersException($"Product {bad.ProductId} is not available.");
        }

        private static void EnsureSameSet(
            IReadOnlyList<ProductQuantityItemDto> requested,
            IReadOnlyList<ProductValidationResultDto> validated)
        {
            var reqSet = requested.GroupBy(x => x.ProductId).Select(g => g.Key).OrderBy(x => x).ToArray();
            var valSet = validated.Select(v => v.ProductId).OrderBy(x => x).ToArray();
            if (reqSet.Length != valSet.Length || !reqSet.SequenceEqual(valSet))
                throw new OrdersException("Catalog validation mismatch.");
        }


        public Task<OrderDetailsDto?> GetOrderByIdAsync(int id, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<OrderListItemDto>> GetOrdersByUserIdAsync(string userId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CancelOrderAsync(int id, string userId, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        private void ValidateProductQuantities(IReadOnlyList<ProductQuantityItemDto> items)
        {
            if (items is null || items.Count == 0)
                throw new OrdersException("No items provided.");

            if (items.Any(item => item.Quantity <= 0))
                throw new OrdersException("All quantities must be positive.");
        }

        private async Task MarkIdempotencyAsFailed(
            OrderIdempotency idempotency,
            string message,
            HttpStatusCode statusCode,
            CancellationToken ct)
        {
            idempotency.Status = IdempotencyStatus.Failed;
            idempotency.ResponseBody = JsonSerializer.Serialize(new { Message = message });
            idempotency.ResponseCode = (int)statusCode;
            await _db.SaveChangesAsync(ct);
        }

        private async Task HandleStockReservationFailure(
            Order order,
            OrderIdempotency idempotency,
            string message,
            HttpStatusCode statusCode,
            CancellationToken ct)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                order.Status = OrderStatus.Rejected;
                order.UpdatedAt = DateTime.UtcNow;

                idempotency.Status = IdempotencyStatus.Failed;
                idempotency.ResponseBody = JsonSerializer.Serialize(new { Message = message });
                idempotency.ResponseCode = (int)statusCode;

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        private static bool IsUniqueConstraintViolationException(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException &&
                   (sqlException.Number == SqlErrorCodes.UniqueConstraintViolation ||
                    sqlException.Number == SqlErrorCodes.UniqueIndexViolation);
        }
    }
}
