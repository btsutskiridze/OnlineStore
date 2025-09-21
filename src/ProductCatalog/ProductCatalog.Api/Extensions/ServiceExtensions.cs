using Polly;
using ProductCatalog.Api.Middleware;
using ProductCatalog.Api.Resilience;
using ProductCatalog.Api.Services;
using ProductCatalog.Api.Services.Contracts;

namespace ProductCatalog.Api.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();
            services.AddOptionsConfiguration(configuration);
            services.AddResiliencePipelines();
            services.AddInternalServices();

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

        private static IServiceCollection AddInternalServices(this IServiceCollection services)
        {
            services.AddScoped<IProductManagementService, ProductManagementService>();
            services.AddScoped<IProductValidationService, ProductValidationService>();
            services.AddScoped<IProductStockService, ProductStockService>();

            return services;
        }

        public static IServiceCollection AddResiliencePipelines(this IServiceCollection services)
        {
            services.AddResiliencePipeline(ResiliencePipelines.DatabaseOperations, DatabaseRetryPipeline.Configure);

            return services;
        }

        public static WebApplication UseGlobalExceptionHandling(this WebApplication app)
        {
            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
            return app;
        }
    }
}
