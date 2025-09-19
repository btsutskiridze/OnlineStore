namespace Orders.Api.Services
{
    public interface ITokenService
    {
        Task<string> GetServiceTokenAsync(string audience, CancellationToken cancellationToken = default);
    }
}
