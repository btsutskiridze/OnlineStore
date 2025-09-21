using Orders.Api.Enums;

namespace Orders.Api.Entities
{
    public sealed class OrderIdempotency
    {
        public long Id { get; set; }
        public string IdempotencyKey { get; set; } = default!;
        public string? RequestHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? OrderId { get; set; }
        public IdempotencyStatus Status { get; set; }
        public int? ResponseCode { get; set; }
        public string? ResponseBody { get; set; }
    }
}
