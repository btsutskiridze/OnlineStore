using Orders.Api.Dtos;

namespace Orders.Api.Http.Clients
{
    public class ProductCatalogClient : IProductCatalogClient
    {
        private readonly HttpClient _http;

        public ProductCatalogClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<List<ProductValidationResultDto>> ValidateAsync(IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct)
        {
            var resp = await _http.PostAsJsonAsync("api/products/validate", items, ct);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<List<ProductValidationResultDto>>(cancellationToken: ct))!;
        }

        public async Task DecrementStockAsync(string idempotencyKey, IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/products/stock/decrement-batch")
            { Content = JsonContent.Create(items) };
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        public async Task ReplenishStockAsync(string idempotencyKey, IReadOnlyList<ProductQuantityItemDto> items, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/products/stock/replenish-batch")
            { Content = JsonContent.Create(items) };
            req.Headers.Add("Idempotency-Key", idempotencyKey);
            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }
    }
}
