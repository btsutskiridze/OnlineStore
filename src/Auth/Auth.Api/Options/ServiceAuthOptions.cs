namespace Auth.Api.Options
{
    public class ServiceAuthOptions
    {
        public List<AuthorizedClient> Clients { get; set; } = [];
    }

    public class AuthorizedClient
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public int TokenExpiryMinutes { get; set; }
        public List<string> AllowedAudiences { get; set; } = [];
    }
}
