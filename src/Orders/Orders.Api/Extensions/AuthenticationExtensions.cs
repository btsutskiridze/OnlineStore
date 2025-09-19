using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Orders.Api.Options;
using System.Security.Claims;
using System.Text;

namespace Orders.Api.Extensions
{
    public static class AuthenticationExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtOptions = configuration.GetSection("JwtTokenValidation").Get<JwtTokenValidationOptions>()
                ?? throw new InvalidOperationException("JwtTokenValidation configuration section is missing");

            services.Configure<JwtTokenValidationOptions>(configuration.GetSection("JwtTokenValidation"));

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

        public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
        {
            services.AddAuthorizationBuilder()
                .AddPolicy("PublicUserAccess", policy =>
                {
                    policy.RequireAssertion(context =>
                        context.User.Claims.Any(claim => claim.Type == "aud" && claim.Value == "OnlineStore") &&
                        context.User.Claims.Any(claim => claim.Type == ClaimTypes.Role));
                });

            return services;
        }
    }
}
