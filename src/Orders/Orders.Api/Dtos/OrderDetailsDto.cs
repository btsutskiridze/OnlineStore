namespace Orders.Api.Dtos
{
    public class OrderDetailsDto
    {
        public int Id { get; set; }
        public string UserId { get; set; } = default!;
        public string Status { get; set; } = default!;
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<OrderItemDetailsDto> Items { get; set; } = new();
    }
}
