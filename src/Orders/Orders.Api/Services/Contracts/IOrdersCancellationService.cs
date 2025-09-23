namespace Orders.Api.Services.Contracts
{
    public interface IOrdersCancellationService
    {
        Task CancelOrderAsync(Guid guid, string rowVersionBase64, CancellationToken ct);
    }
}
