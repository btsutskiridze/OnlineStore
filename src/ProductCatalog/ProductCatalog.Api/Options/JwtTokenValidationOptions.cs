namespace ProductCatalog.Api.Options
{
    public class JwtTokenValidationOptions
    {
        public string Issuer { get; set; } = string.Empty;
        public List<string> AcceptedAudiences { get; set; } = new();
        public string SigningKey { get; set; } = string.Empty;
    }
}
