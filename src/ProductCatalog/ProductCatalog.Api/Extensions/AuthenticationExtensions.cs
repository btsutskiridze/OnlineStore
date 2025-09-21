using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProductCatalog.Api.Options;
using System.Text;

namespace ProductCatalog.Api.Extensions
{
    public static class AuthenticationExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()
                ?? throw new InvalidOperationException("Jwt configuration section is missing");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudiences = jwtOptions.AcceptedAudiences,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    };
                });

            return services;
        }

        public static IServiceCollection AddCustomAuthorization(this IServiceCollection services, IConfiguration configuration)
        {
            var authOptions = configuration.GetSection("InboundServiceAuth").Get<InboundServiceAuthOptions>()
                ?? throw new InvalidOperationException("InboundServiceAuth configuration section is missing");

            services.AddAuthorizationBuilder()
                .AddPolicy(Policies.InterServiceAccessOnly, policy =>
                {
                    policy.RequireAssertion(context =>
                        context.User.Claims.Any(claim => claim.Type == "aud" && claim.Value == authOptions.Audience) &&
                        context.User.Claims.Any(claim => claim.Type == "client_id" && authOptions.AllowedClientIds.Contains(claim.Value))
                    );
                });

            return services;
        }
    }
}
