namespace Orders.Api.Exceptions
{
    public class OrderNotFoundException : OrdersException
    {
        public OrderNotFoundException(int orderId) : base($"Order with ID {orderId} was not found.")
        {
        }
    }
}
