using Orders.Api.Dtos;
using Orders.Api.Entities;

namespace Orders.Api.Mappers
{
    public static class OrdersMapper
    {
        public static OrderDetailsDto ToOrderDetailsDto(this Order order)
        {
            return new()
            {
                Guid = order.Guid,
                UserId = order.UserId,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                Status = order.Status.ToString(),
                Items = order.Items.Select(i => new OrderItemDetailsDto
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    SKU = i.SKU,
                    LineTotal = i.LineTotal
                }).ToList()
            };
        }

        public static OrderListItemDto ToOrderListItemDto(this Order order)
        {
            return new()
            {
                Guid = order.Guid,
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt,
                Status = order.Status.ToString(),
                ItemCount = order.Items.Count,
            };
        }
    }
}
