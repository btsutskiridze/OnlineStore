using Orders.Api.Middleware;

namespace Orders.Api.Extensions
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
            services.Configure<Options.JwtOptions>(
                configuration.GetSection("Jwt"));

            services.Configure<Options.ServicesOptions>(
                configuration.GetSection("Services"));

            services.Configure<Options.ServiceAuthOptions>(
                configuration.GetSection("ServiceAuth"));

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
