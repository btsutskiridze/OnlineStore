namespace ProductCatalog.Api.Exceptions
{
    public class ConcurrencyConflictException : ProductCatalogException
    {
        public ConcurrencyConflictException() : base("Concurrency update conflict occurred.")
        {
        }
    }
}
