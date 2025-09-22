using Microsoft.Extensions.Options;
using Orders.Api.Http.Clients;
using Orders.Api.Http.Handlers;
using Orders.Api.Middleware;
using Orders.Api.Options;
using Orders.Api.Services;
using Orders.Api.Services.Contracts;
using Polly;
using System.Net;

namespace Orders.Api.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();
            services.AddOptionsConfiguration(configuration);
            services.AddProductCatalogClient(configuration);
            services.AddInternalServices();

            return services;
        }

        private static IServiceCollection AddOptionsConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JwtOptions>(
                configuration.GetSection("Jwt"));

            services.Configure<ServicesOptions>(
                configuration.GetSection("Services"));

            services.Configure<ServiceAuthOptions>(
                configuration.GetSection("ServiceAuth"));

            return services;
        }

        public static WebApplication UseGlobalExceptionHandling(this WebApplication app)
        {
            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
            return app;
        }

        public static IServiceCollection AddInternalServices(this IServiceCollection services)
        {
            services.AddScoped<IOrdersReadService, OrdersReadService>();
            services.AddScoped<IOrdersCreationService, OrdersCreationService>();
            services.AddScoped<IOrdersCancellationService, OdersCancellationService>();
            return services;
        }


        public static IServiceCollection AddProductCatalogClient(this IServiceCollection services, IConfiguration config)
        {

            services.AddHttpClient("Auth", (sp, http) =>
            {
                var opt = sp.GetRequiredService<IOptions<ServicesOptions>>().Value;
                http.BaseAddress = new Uri(opt.Auth.BaseUrl);
                http.Timeout = TimeSpan.FromSeconds(5);
            });

            services.AddTransient<ServiceAuthHandler>();

            var pc = services.AddHttpClient<IProductCatalogClient, ProductCatalogClient>((sp, http) =>
            {
                var opt = sp.GetRequiredService<IOptions<ServicesOptions>>().Value;
                http.BaseAddress = new Uri(opt.ProductCatalog.BaseUrl);
                http.Timeout = TimeSpan.FromSeconds(10);
            });
            pc.AddStandardResilienceHandler(o =>
            {
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(12);
                o.Retry.MaxRetryAttempts = 3;
                o.Retry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(response =>
                        response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                        response.StatusCode == HttpStatusCode.GatewayTimeout ||
                        response.StatusCode == HttpStatusCode.BadGateway ||
                        response.StatusCode == HttpStatusCode.RequestTimeout ||
                        response.StatusCode == HttpStatusCode.TooManyRequests)
                    .Handle<TimeoutException>();
            });

            pc.AddHttpMessageHandler<ServiceAuthHandler>();

            return services;
        }
    }
}
