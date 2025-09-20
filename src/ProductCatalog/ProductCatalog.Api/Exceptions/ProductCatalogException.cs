namespace ProductCatalog.Api.Exceptions
{
    public abstract class ProductCatalogException : Exception
    {
        protected ProductCatalogException(string message) : base(message)
        {
        }

        protected ProductCatalogException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
