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
        public List<string> AllowedAudiences { get; set; } = [];
    }
}
