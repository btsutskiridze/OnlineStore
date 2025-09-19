namespace ProductCatalog.Domain.Exceptions
{
    public class ProductOutOfStockException : ProductCatalogException
    {
        public ProductOutOfStockException(int productId) : base($"Product with ID {productId} is out of stock.")
        {
        }
    }
}
