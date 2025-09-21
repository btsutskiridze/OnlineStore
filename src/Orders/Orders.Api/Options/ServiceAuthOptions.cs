namespace Orders.Api.Options
{
    public class ServiceAuthOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public int TokenExpiryMinutes { get; set; }
        public List<string> AllowedAudiences { get; set; } = new();
    }
}


