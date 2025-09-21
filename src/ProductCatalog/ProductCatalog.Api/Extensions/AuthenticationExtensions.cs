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
            var jwtOptions = configuration.GetSection("JwtTokenValidation").Get<JwtTokenValidationOptions>()
                ?? throw new InvalidOperationException("JwtTokenValidation configuration section is missing");

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
            var authOptions = configuration.GetSection("InboundServiceAuthorization").Get<InboundServiceAuthorizationOptions>()
                ?? throw new InvalidOperationException("InboundServiceAuthorization configuration section is missing");

            var interServiceConfig = authOptions.InterServiceAccess;

            services.AddAuthorizationBuilder()
                .AddPolicy(Policies.InterServiceAccessOnly, policy =>
                {
                    policy.RequireAssertion(context =>
                        context.User.Claims.Any(claim => claim.Type == "aud" && claim.Value == interServiceConfig.RequiredServiceAudience) &&
                        context.User.Claims.Any(claim => claim.Type == "client_id" && claim.Value == interServiceConfig.AuthorizedClientId) &&
                        interServiceConfig.RequiredPermissions.Any(permission =>
                            context.User.Claims.Any(claim => claim.Type == "permission" && claim.Value == permission))
                    );
                });

            return services;
        }
    }
}
