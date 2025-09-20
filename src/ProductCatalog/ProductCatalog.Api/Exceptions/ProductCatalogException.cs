namespace ProductCatalog.Api.Exceptions
{
    public class ProductCatalogException : Exception
    {
        public ProductCatalogException(string message) : base(message)
        {
        }

        protected ProductCatalogException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
