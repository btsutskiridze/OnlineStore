using ProductCatalog.Api.Enums;

namespace ProductCatalog.Api.Entities
{
    public sealed class InventoryOperation
    {
        public long Id { get; set; }
        public string IdempotencyKey { get; set; } = default!;
        public InventoryOperationType Type { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
