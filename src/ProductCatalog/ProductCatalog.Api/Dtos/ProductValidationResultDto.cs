namespace ProductCatalog.Api.Dtos
{
    public class ProductValidationResultDto
    {
        public int ProductId { get; init; }
        public int RequestedQuantity { get; init; }
        public bool Exists { get; init; }
        public bool CanFulfill { get; init; }
        public string? Name { get; init; }
        public string? Sku { get; init; }
        public decimal? Price { get; init; }
    }
}
