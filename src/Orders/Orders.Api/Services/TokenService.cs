using Orders.Api.Options;
using System.Text.Json;

namespace Orders.Api.Services
{
    public class TokenService : ITokenService
    {
        private readonly HttpClient _httpClient;
        private readonly OutboundServiceAuthentication _serviceAuth;
        private readonly ILogger<TokenService> _logger;

        public TokenService(HttpClient httpClient, IConfiguration configuration, ILogger<TokenService> logger)
        {
            _httpClient = httpClient;
            _serviceAuth = configuration.GetSection("OutboundServiceAuthentication").Get<OutboundServiceAuthentication>()
                ?? throw new InvalidOperationException("OutboundServiceAuthentication configuration is missing");
            _logger = logger;
        }

        public async Task<string> GetServiceTokenAsync(string audience, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, _serviceAuth.AuthenticationServiceUrl);
                request.Headers.Add("X-Client-Id", _serviceAuth.ClientCredentials.ClientId);
                request.Headers.Add("X-Client-Secret", _serviceAuth.ClientCredentials.ClientSecret);
                request.Headers.Add("X-Audience", audience);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(content);

                if (document.RootElement.TryGetProperty("token", out var tokenElement))
                {
                    return tokenElement.GetString() ?? throw new InvalidOperationException("Token is null");
                }

                throw new InvalidOperationException("Token not found in response");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get service token for audience {Audience}", audience);
                throw;
            }
        }
    }
}
