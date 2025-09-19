namespace Auth.Api.Options
{
    public class InterServiceAuthenticationOptions
    {
        public List<AuthorizedClient> AuthorizedClients { get; set; } = [];
    }

    public class AuthorizedClient
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public List<string> AllowedTargetServices { get; set; } = [];
        public List<string> GrantedPermissions { get; set; } = [];
    }
}
