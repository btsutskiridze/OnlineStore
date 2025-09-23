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
    public class OrdersCreationService : IOrdersCreationService
    {
        private const int CONCURRENT_REQUEST_WINDOW_SECONDS = 45;
        private readonly string _currentUserId;
        private readonly IProductCatalogClient _catalogClient;
        private readonly AppDbContext _context;

        public OrdersCreationService(IHttpContextAccessor httpContextAccessor, IProductCatalogClient productCatalogClient, AppDbContext dbContext)
        {
            _currentUserId = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                throw new UnauthorizedAccessException("User is not authenticated.");
            _catalogClient = productCatalogClient;
            _context = dbContext;
        }

        public async Task<OrderDetailsDto> CreateOrderAsync(string idempotencyKey, IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct)
        {
            ValidateCreateOrderRequest(idempotencyKey, items);

            var consolidatedItems = ConsolidateProductQuantities(items);
            var requestHash = GenerateRequestHash(consolidatedItems);

            var (idempotency, existingOrder) = await HandleIdempotencyCheck(idempotencyKey, requestHash, ct);

            if (existingOrder is not null)
                return existingOrder;


            Order pendingOrder;
            try
            {
                var validatedProducts = await ValidateProductAvailability(consolidatedItems, ct);
                pendingOrder = await CreatePendingOrder(validatedProducts, ct);
            }
            catch
            {
                await HandleOrderValidationFailure(idempotency!, "Stock validation failed", HttpStatusCode.Conflict, ct);
                throw new OrdersException("Order products validation failed");
            }

            try
            {
                var reserveKey = $"order:{pendingOrder.Guid}";
                await ReserveProductStock(reserveKey, consolidatedItems, ct);
            }
            catch
            {
                await HandleStockReservationFailure(pendingOrder, idempotency!, "Stock reservation failed", HttpStatusCode.Conflict, ct);
                throw new OrdersException("Cannot reserve products. stock may have changed while you were ordering.");
            }

            var orderDetails = pendingOrder.ToOrderDetailsDto();
            await FinalizeSuccessfulOrder(idempotency!, pendingOrder.Id, orderDetails, ct);

            return orderDetails;
        }

        private async Task<(OrderIdempotency?, OrderDetailsDto?)> HandleIdempotencyCheck(string idempotencyKey, string requestFingerprint, CancellationToken ct)
        {
            var existingIdempotency = await _context.OrderIdempotencies
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);

            if (existingIdempotency is not null)
            {
                return await ProcessExistingIdempotencyRecord(existingIdempotency, requestFingerprint, ct);
            }

            var newIdempotency = await CreateNewIdempotencyRecord(idempotencyKey, requestFingerprint, ct);
            return (newIdempotency, null);
        }

        private async Task<(OrderIdempotency?, OrderDetailsDto?)> ProcessExistingIdempotencyRecord(OrderIdempotency idempotency, string requestFingerprint, CancellationToken ct)
        {
            if (idempotency.RequestHash is not null && !string.Equals(idempotency.RequestHash, requestFingerprint, StringComparison.Ordinal))
                throw new OrdersException("This idempotency key was used before with different data.");

            switch (idempotency.Status)
            {
                case IdempotencyStatus.Completed:
                    if (string.IsNullOrEmpty(idempotency.ResponseBody) || idempotency.ResponseCode != (int)HttpStatusCode.Created)
                        throw new OrdersException("Cannot replay this request - response data missing.");

                    return (null, JsonSerializer.Deserialize<OrderDetailsDto>(idempotency.ResponseBody)!);

                case IdempotencyStatus.Failed:
                    throw new OrdersException("Previous request with same idempotency key failed.");

                case IdempotencyStatus.Started:
                    await HandleConcurrentRequest(idempotency, ct);
                    break;
            }

            return (idempotency, null);
        }

        private async Task HandleConcurrentRequest(OrderIdempotency idempotency, CancellationToken ct)
        {
            var requestAge = DateTime.UtcNow - (idempotency.UpdatedAt ?? idempotency.CreatedAt);
            if (requestAge < TimeSpan.FromSeconds(CONCURRENT_REQUEST_WINDOW_SECONDS))
            {
                throw new OrdersException("Another request with same idempotency key is still processing");
            }

            idempotency.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        private async Task<OrderIdempotency> CreateNewIdempotencyRecord(string idempotencyKey, string requestFingerprint, CancellationToken ct)
        {
            try
            {
                var newIdempotency = new OrderIdempotency
                {
                    IdempotencyKey = idempotencyKey,
                    RequestHash = requestFingerprint,
                    Status = IdempotencyStatus.Started,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.OrderIdempotencies.Add(newIdempotency);
                await _context.SaveChangesAsync(ct);
                return newIdempotency;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                throw new OrdersException("Same request is already running. Try again later.");
            }
        }

        private async Task<List<ProductValidationResultDto>> ValidateProductAvailability(List<ProductQuantityItemDto> items, CancellationToken ct)
        {
            var validatedProducts = await _catalogClient.ValidateAsync(items, ct);

            ValidateAllProductsAvailable(validatedProducts);
            ValidateProductSetMatches(items, validatedProducts);

            return validatedProducts;
        }

        private async Task<Order> CreatePendingOrder(List<ProductValidationResultDto> validatedProducts, CancellationToken ct)
        {
            var order = BuildOrderFromValidatedProducts(validatedProducts);

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return order;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw new OrdersException("Failed to create order");
            }
        }

        private async Task ReserveProductStock(string idempotencyKey, List<ProductQuantityItemDto> items, CancellationToken ct)
        {
            await _catalogClient.DecrementStockAsync(idempotencyKey, items, ct);
        }

        private async Task FinalizeSuccessfulOrder(OrderIdempotency idempotency, int orderId, OrderDetailsDto orderDetails, CancellationToken ct)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            idempotency.Status = IdempotencyStatus.Completed;
            idempotency.OrderId = orderId;
            idempotency.ResponseBody = JsonSerializer.Serialize(orderDetails);
            idempotency.ResponseCode = (int)HttpStatusCode.Created;
            idempotency.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        private Order BuildOrderFromValidatedProducts(List<ProductValidationResultDto> validatedProducts)
        {
            var orderItems = validatedProducts.Select(product => new OrderItem
            {
                ProductId = product.ProductId,
                ProductName = product.Name!,
                UnitPrice = (decimal)product.Price!,
                Quantity = product.RequestedQuantity,
                SKU = product.Sku!,
                LineTotal = (decimal)product.Price! * product.RequestedQuantity,
            }).ToList();

            var order = new Order
            {
                Guid = Guid.NewGuid(),
                UserId = _currentUserId,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Items = orderItems,
                TotalAmount = orderItems.Sum(item => item.LineTotal)
            };

            return order;
        }

        private static string GenerateRequestHash(IReadOnlyList<ProductQuantityItemDto> items)
        {
            var fingerprint = new StringBuilder();
            foreach (var item in items)
                fingerprint.Append(item.ProductId).Append(':').Append(item.Quantity).Append(';');
            return HashUtils.ComputeSha256Hash(fingerprint.ToString());
        }

        private static void ValidateCreateOrderRequest(string idempotencyKey, IReadOnlyList<ProductQuantityItemDto> items)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new OrdersException("Idempotency key is required.");

            if (items is null || items.Count == 0)
                throw new OrdersException("No items provided.");

            if (items.Any(item => item.Quantity <= 0))
                throw new OrdersException("All quantities must be positive.");
        }

        private static List<ProductQuantityItemDto> ConsolidateProductQuantities(IReadOnlyList<ProductQuantityItemDto> items)
        {
            return items
                .GroupBy(item => item.ProductId)
                .Select(group => new ProductQuantityItemDto
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(x => x.Quantity)
                })
                .OrderBy(item => item.ProductId)
                .ToList();
        }

        private static void ValidateAllProductsAvailable(IEnumerable<ProductValidationResultDto> validatedProducts)
        {
            var unavailableProduct = validatedProducts.FirstOrDefault(product => !product.CanFulfill);
            if (unavailableProduct is not null)
                throw new OrdersException($"Product {unavailableProduct.ProductId} is not available.");
        }

        private static void ValidateProductSetMatches(
            IReadOnlyList<ProductQuantityItemDto> requestedItems,
            IReadOnlyList<ProductValidationResultDto> validatedProducts)
        {
            var requestedProductIds = requestedItems.GroupBy(x => x.ProductId).Select(g => g.Key).OrderBy(x => x).ToArray();
            var validatedProductIds = validatedProducts.Select(v => v.ProductId).OrderBy(x => x).ToArray();

            if (requestedProductIds.Length != validatedProductIds.Length || !requestedProductIds.SequenceEqual(validatedProductIds))
                throw new OrdersException("Product catalog validation mismatch.");
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException &&
                   (sqlException.Number == SqlErrorCodes.UniqueConstraintViolation ||
                    sqlException.Number == SqlErrorCodes.UniqueIndexViolation);
        }

        private async Task HandleStockReservationFailure(
            Order order,
            OrderIdempotency idempotency,
            string message,
            HttpStatusCode statusCode,
            CancellationToken ct)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                order.Status = OrderStatus.Rejected;
                order.UpdatedAt = DateTime.UtcNow;
                idempotency.Status = IdempotencyStatus.Failed;
                idempotency.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        private async Task HandleOrderValidationFailure(
            OrderIdempotency idempotency,
            string message,
            HttpStatusCode statusCode,
            CancellationToken ct)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                idempotency.Status = IdempotencyStatus.Failed;
                idempotency.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }
}
