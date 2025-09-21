namespace Orders.Api.Options
{
    public class ServicesOptions
    {
        public ServiceEndpoint ProductCatalog { get; set; } = new();
        public ServiceEndpoint Auth { get; set; } = new();
    }

    public class ServiceEndpoint
    {
        public string BaseUrl { get; set; } = string.Empty;
    }
}


