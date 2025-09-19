namespace ProductCatalog.Api.Responses
{
    public class ApiErrorResponse
    {
        public string Title { get; set; } = string.Empty;
        public int Status { get; set; }
        public string Detail { get; set; } = string.Empty;
        public string? Instance { get; set; }
        public string TraceId { get; set; } = string.Empty;
        public Dictionary<string, object>? Extensions { get; set; }
    }
}
