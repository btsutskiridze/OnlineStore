namespace Orders.Api.Exceptions
{
    public class OrdersException : Exception
    {
        public OrdersException(string message) : base(message)
        {
        }

        public OrdersException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
