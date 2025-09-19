namespace ProductCatalog.Api.Options
{
    public class InboundServiceAuthorizationOptions
    {
        public InterServiceAccessConfiguration InterServiceAccess { get; set; } = new();
    }

    public class InterServiceAccessConfiguration
    {
        public string RequiredServiceAudience { get; set; } = string.Empty;
        public string AuthorizedClientId { get; set; } = string.Empty;
        public List<string> RequiredPermissions { get; set; } = [];
    }
}
