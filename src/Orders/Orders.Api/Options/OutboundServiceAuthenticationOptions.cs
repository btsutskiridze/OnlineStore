namespace Orders.Api.Options
{
    public class OutboundServiceAuthentication
    {
        public string AuthenticationServiceUrl { get; set; } = string.Empty;
        public ClientCredentials ClientCredentials { get; set; } = new();
        public Dictionary<string, TargetServiceConfig> TargetServices { get; set; } = new();
    }

    public class ClientCredentials
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }

    public class TargetServiceConfig
    {
        public string ServiceAudience { get; set; } = string.Empty;
        public List<string> RequiredPermissions { get; set; } = new();
    }
}
