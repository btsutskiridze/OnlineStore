using Orders.Api.Services.Contracts;

namespace Orders.Api.Services
{
    public class OdersCancellationService : IOrdersCancellationService
    {
        public Task<bool> CancelOrderAsync(Guid guid, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
