namespace Orders.Api.Exceptions
{
    public abstract class OrdersException : Exception
    {
        protected OrdersException(string message) : base(message)
        {
        }

        protected OrdersException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
