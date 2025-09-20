namespace ProductCatalog.Api.Exceptions
{
    public class ProductOutOfStockException : ProductCatalogException
    {
        public ProductOutOfStockException(int productId) : base($"Product with ID {productId} is out of stock.")
        {
        }
    }
}
