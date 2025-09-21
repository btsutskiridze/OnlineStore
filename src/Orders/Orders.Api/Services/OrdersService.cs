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

        public async Task<OrderDetailsDto> CreateOrderAsync(string idempotencyKey, IReadOnlyList<CreateOrderItemDto> items, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new OrdersException("Idempotency key is required.");
            ValidateProductQuantities(items);

            var distinctItems = items
                .GroupBy(i => i.ProductId)
                .Select(g => new CreateOrderItemDto { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderBy(x => x.ProductId)
                .ToList();

            var requestHash = HashUtils.ComputeSha256Hash(JsonSerializer.Serialize(distinctItems));

            var idempotency = await _db.OrderIdempotencies
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

            if (idempotency is not null)
            {
                if (idempotency.RequestHash is not null && !string.Equals(idempotency.RequestHash, requestHash, StringComparison.Ordinal))
                    throw new OrdersException("Idempotency key has already been used with different request parameters.");

                switch (idempotency.Status)
                {
                    case IdempotencyStatus.Succeeded:
                        if (string.IsNullOrEmpty(idempotency.ResponseBody))
                            throw new OrdersException("Idempotent replay has no stored response.");
                        if (idempotency.ResponseCode != (int)HttpStatusCode.Created)
                            throw new OrdersException("Idempotent replay has an invalid response.");
                        var dto = JsonSerializer.Deserialize<OrderDetailsDto>(idempotency.ResponseBody)!;
                        return dto;
                    case IdempotencyStatus.Failed:
                        throw new OrdersException("The previous operation with the same idempotency key failed.");
                    case IdempotencyStatus.InProgress:
                        throw new OrdersException("An operation with the same idempotency key is already in progress. Please try again later.");
                    default:
                        throw new OrdersException("Unknown idempotency status.");
                }
            }
            else
            {
                try
                {
                    idempotency = new()
                    {
                        IdempotencyKey = idempotencyKey,
                        RequestHash = requestHash,
                        Status = IdempotencyStatus.InProgress,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.OrderIdempotencies.Add(idempotency);
                    await _db.SaveChangesAsync(ct);

                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolationException(ex))
                {
                    throw new OrdersException("An operation with the same idempotency key is already in progress. Please try again later.");
                }
            }

            var requestForCatalog = distinctItems
                .Select(i => new ProductQuantityItemDto { ProductId = i.ProductId, Quantity = i.Quantity })
                .ToList();

            List<ProductValidationResultDto> productItems;
            try
            {
                productItems = await _productCatalogClient.ValidateAsync(requestForCatalog, ct);

                if (productItems.Any(r => !r.CanFulfill))
                {
                    await MarkIdempotencyAsFailed(idempotency, "One or more products are invalid or have insufficient stock.", HttpStatusCode.BadRequest, ct);
                    throw new OrdersException("One or more products are invalid or have insufficient stock.");
                }
            }
            catch (OrdersException)
            {
                throw;
            }
            catch (Exception)
            {
                await MarkIdempotencyAsFailed(idempotency, "Failed to validate products with catalog service.", HttpStatusCode.ServiceUnavailable, ct);
                throw new OrdersException("Failed to validate products. Please try again later.");
            }

            Order order;
            await using (var transaction = await _db.Database.BeginTransactionAsync(ct))
            {
                order = new Order
                {
                    Guid = Guid.NewGuid(),
                    UserId = _userId,
                    Status = OrderStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    Items = productItems.Select(pi => new OrderItem
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

                _db.Orders.Add(order);
                await _db.SaveChangesAsync(ct);

                idempotency.OrderId = order.Id;
                await _db.SaveChangesAsync(ct);

                await transaction.CommitAsync(ct);
            }

            try
            {
                var reserveKey = $"order:{idempotencyKey}";
                await _productCatalogClient.DecrementStockAsync(reserveKey, requestForCatalog, ct);

                await using var transaction = await _db.Database.BeginTransactionAsync(ct);
                try
                {
                    order.Status = OrderStatus.Confirmed;
                    order.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            }
            catch (HttpRequestException)
            {
                await HandleStockReservationFailure(order, idempotency, "Failed to reserve stock for the order.", HttpStatusCode.Conflict, ct);
                throw new OrdersException("Failed to reserve stock for the order.");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                await HandleStockReservationFailure(order, idempotency, "Stock reservation request timed out.", HttpStatusCode.RequestTimeout, ct);
                throw new OrdersException("Stock reservation request timed out.");
            }
            catch (Exception)
            {
                await HandleStockReservationFailure(order, idempotency, "An error occurred while reserving stock for the order.", HttpStatusCode.InternalServerError, ct);
                throw new OrdersException("An error occurred while reserving stock for the order.");
            }

            var orderDetailsDto = order.ToOrderDetailsDto();

            idempotency.Status = IdempotencyStatus.Succeeded;
            idempotency.ResponseBody = JsonSerializer.Serialize(orderDetailsDto);
            idempotency.ResponseCode = (int)HttpStatusCode.Created;
            await _db.SaveChangesAsync(ct);

            return orderDetailsDto;
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

        private void ValidateProductQuantities(IReadOnlyList<CreateOrderItemDto> items)
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
