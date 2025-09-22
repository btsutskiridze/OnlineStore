namespace Orders.Api.Services.Contracts
{
    public interface IOrdersCancellationService
    {
        Task<bool> CancelOrderAsync(Guid guid, CancellationToken ct);
    }
}
