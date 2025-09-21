namespace ProductCatalog.Api.Options
{
    public class InboundServiceAuthOptions
    {
        public string Audience { get; set; } = string.Empty;
        public List<string> AllowedClientIds { get; set; } = [];
    }
}


