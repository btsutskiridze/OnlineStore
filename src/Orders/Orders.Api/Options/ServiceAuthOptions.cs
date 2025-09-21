namespace Orders.Api.Options
{
    public class ServiceAuthOptions
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public List<string> AllowedAudiences { get; set; } = new();
    }
}


