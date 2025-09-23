namespace ProductCatalog.Api.Exceptions
{
    public class ProductSkuConflictException : ProductCatalogException
    {
        public ProductSkuConflictException(string sku) : base($"SKU '{sku}' is already used by another product.")
        {
        }
    }
}
