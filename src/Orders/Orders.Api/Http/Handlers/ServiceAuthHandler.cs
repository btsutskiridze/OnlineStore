using Microsoft.Extensions.Options;
using Orders.Api.Options;
using System.Net.Http.Headers;

namespace Orders.Api.Http.Handlers
{
    public class ServiceAuthHandler : DelegatingHandler
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ServicesOptions _services;
        private readonly ServiceAuthOptions _auth;

        // caching for resilience retries
        private string? _cachedToken;
        private DateTime _expiresUtc;

        public ServiceAuthHandler(
            IOptions<ServicesOptions> services,
            IOptions<ServiceAuthOptions> auth,
            IHttpClientFactory httpClientFactory)
        {
            _services = services.Value;
            _auth = auth.Value;
            _httpClientFactory = httpClientFactory;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            await EnsureTokenAsync(ct);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);

            var resp = await base.SendAsync(req, ct);
            return resp;
        }

        private async Task EnsureTokenAsync(CancellationToken ct, bool forceRefresh = false)
        {
            if (!forceRefresh && _cachedToken is not null && DateTime.UtcNow < _expiresUtc.AddMinutes(-5))
                return;

            var client = _httpClientFactory.CreateClient("Auth");
            var audience = _auth.AllowedAudiences[0];
            using var authReq = new HttpRequestMessage(HttpMethod.Post, $"{_services.Auth.BaseUrl}/internal/token");
            authReq.Headers.Add("X-Client-Id", _auth.ClientId);
            authReq.Headers.Add("X-Client-Secret", _auth.ClientSecret);
            authReq.Headers.Add("X-Audience", audience);

            var resp = await client.SendAsync(authReq, ct);
            resp.EnsureSuccessStatusCode();
            var payload = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
            _cachedToken = payload!.token;

            _expiresUtc = DateTime.UtcNow.AddMinutes(_auth.TokenExpiryMinutes);
        }

        private sealed class TokenResponse { public string token { get; set; } = default!; }
    }
}
