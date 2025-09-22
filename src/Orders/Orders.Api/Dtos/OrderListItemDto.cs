namespace Orders.Api.Dtos
{
    public class OrderListItemDto
    {
        public Guid Guid { get; init; }
        public DateTime CreatedAt { get; init; }
        public decimal TotalAmount { get; init; }
        public int ItemCount { get; init; }
        public string Status { get; init; } = default!;
    }
}
