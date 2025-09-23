namespace Orders.Api.Exceptions
{
    public class InsufficientStockException : OrdersException
    {
        public InsufficientStockException(int productId, int requestedQuantity, int availableQuantity)
            : base($"Not enough stock for product {productId}. Requested: {requestedQuantity}, Available: {availableQuantity}")
        {
        }
    }
}
