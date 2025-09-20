using ProductCatalog.Api.Middleware;

namespace ProductCatalog.Api.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();
            services.AddOptionsConfiguration(configuration);
            services.AddHttpClients(configuration);
            services.AddExternalServiceClients(configuration);

            return services;
        }

        private static IServiceCollection AddOptionsConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<Options.JwtTokenValidationOptions>(
                configuration.GetSection("JwtTokenValidation"));

            services.Configure<Options.InboundServiceAuthorizationOptions>(
                configuration.GetSection("InboundServiceAuthorization"));

            return services;
        }

        private static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
        {
            return services;
        }

        public static IServiceCollection AddExternalServiceClients(this IServiceCollection services, IConfiguration configuration)
        {
            return services;
        }


        public static WebApplication UseGlobalExceptionHandling(this WebApplication app)
        {
            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
            return app;
        }
    }
}
