using System.Net.Http.Headers;
using System.Text.Json;

namespace Orders.Api.Services
{
    public class ProductCatalogService : IProductCatalogService
    {
        private readonly HttpClient _httpClient;
        private readonly ITokenService _tokenService;
        private readonly ILogger<ProductCatalogService> _logger;

        public ProductCatalogService(HttpClient httpClient, ITokenService tokenService, ILogger<ProductCatalogService> logger)
        {
            _httpClient = httpClient;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<bool> DecrementStockAsync(int productId, int quantity, CancellationToken cancellationToken = default)
        {
            try
            {
                var token = await _tokenService.GetServiceTokenAsync("ProductCatalog", cancellationToken);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var requestBody = new { ProductId = productId, Quantity = quantity };
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/stock/decrement", content, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrement stock for product {ProductId}, quantity {Quantity}", productId, quantity);
                return false;
            }
        }

        public async Task<bool> IsProductAvailableAsync(int productId, int quantity, CancellationToken cancellationToken = default)
        {
            try
            {
                var token = await _tokenService.GetServiceTokenAsync("ProductCatalog", cancellationToken);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"/api/products/{productId}/availability?quantity={quantity}", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var document = JsonDocument.Parse(content);

                    if (document.RootElement.TryGetProperty("available", out var availableElement))
                    {
                        return availableElement.GetBoolean();
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check availability for product {ProductId}, quantity {Quantity}", productId, quantity);
                return false;
            }
        }
    }
}
