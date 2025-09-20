namespace ProductCatalog.Api.Exceptions
{
    public class ProductSkuConflictException : ProductCatalogException
    {
        public ProductSkuConflictException(string sku) : base($"A product with SKU '{sku}' already exists.")
        {
        }
    }
}
