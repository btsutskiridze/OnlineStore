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
        private readonly ILogger<ServiceAuthHandler> _logger;

        // caching for resilience retries
        private string? _cachedToken;
        private DateTime _expiresUtc;

        public ServiceAuthHandler(
            IOptions<ServicesOptions> services,
            IOptions<ServiceAuthOptions> auth,
            IHttpClientFactory httpClientFactory,
            ILogger<ServiceAuthHandler> logger)
        {
            _services = services.Value;
            _auth = auth.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
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

            try
            {
                var client = _httpClientFactory.CreateClient("Auth");
                var audience = _auth.AllowedAudiences[0];
                
                _logger.LogInformation("Requesting service token for audience: {Audience} from {BaseUrl}", 
                    audience, client.BaseAddress);
                
                using var authReq = new HttpRequestMessage(HttpMethod.Post, "/auth/internal/token");
                authReq.Headers.Add("X-Client-Id", _auth.ClientId);
                authReq.Headers.Add("X-Client-Secret", _auth.ClientSecret);
                authReq.Headers.Add("X-Audience", audience);

                var resp = await client.SendAsync(authReq, ct);
                
                if (!resp.IsSuccessStatusCode)
                {
                    var errorContent = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Auth service returned {StatusCode}: {Error}", resp.StatusCode, errorContent);
                    resp.EnsureSuccessStatusCode();
                }
                
                var payload = await resp.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
                _cachedToken = payload!.token;
                _expiresUtc = DateTime.UtcNow.AddMinutes(_auth.TokenExpiryMinutes);
                
                _logger.LogInformation("Successfully obtained service token, expires at: {ExpiresAt}", _expiresUtc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to obtain service token from auth service");
                throw;
            }
        }

        private sealed class TokenResponse { public string token { get; set; } = default!; }
    }
}
