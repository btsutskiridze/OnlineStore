using Orders.Api.Enums;

namespace Orders.Api.Entities
{
    public sealed class Order
    {
        public int Id { get; set; }
        public Guid Guid { get; set; } = Guid.Empty;
        public string UserId { get; set; } = default!;
        public OrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public byte[] RowVersion { get; set; } = default!;
        public List<OrderItem> Items { get; set; } = new();
    }
}
