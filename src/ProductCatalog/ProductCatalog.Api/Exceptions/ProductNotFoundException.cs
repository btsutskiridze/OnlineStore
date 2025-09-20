namespace ProductCatalog.Api.Exceptions
{
    public class ProductNotFoundException : ProductCatalogException
    {
        public ProductNotFoundException(int productId) : base($"Product with ID {productId} was not found.")
        {
        }
    }
}
