namespace Orders.Api.Exceptions
{
    public class OrderNotFoundException : OrdersException
    {
        public OrderNotFoundException(Guid orderId) : base($"Order with ID {orderId} was not found.")
        {
        }
    }
}
