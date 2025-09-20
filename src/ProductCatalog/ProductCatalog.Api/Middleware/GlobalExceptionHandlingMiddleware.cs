using ProductCatalog.Api.Exceptions;
using ProductCatalog.Api.Responses;
using System.Net;
using System.Text.Json;

namespace ProductCatalog.Api.Middleware
{
    public class GlobalExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

        public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred while processing the request");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = context.Response;
            response.ContentType = "application/json";

            var errorResponse = CreateErrorResponse(context, exception);
            response.StatusCode = errorResponse.Status;

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonResponse = JsonSerializer.Serialize(errorResponse, jsonOptions);
            await response.WriteAsync(jsonResponse);
        }

        private static ApiErrorResponse CreateErrorResponse(HttpContext context, Exception exception)
        {
            var traceId = context.TraceIdentifier;
            var instance = context.Request.Path;

            return exception switch
            {
                ProductNotFoundException ex => new ApiErrorResponse
                {
                    Title = "Product Not Found",
                    Status = (int)HttpStatusCode.NotFound,
                    Detail = ex.Message,
                    Instance = instance,
                    TraceId = traceId
                },

                ProductOutOfStockException ex => new ApiErrorResponse
                {
                    Title = "Product Out of Stock",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = ex.Message,
                    Instance = instance,
                    TraceId = traceId
                },

                ProductCatalogException ex => new ApiErrorResponse
                {
                    Title = "Product Catalog Error",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = ex.Message,
                    Instance = instance,
                    TraceId = traceId
                },

                UnauthorizedAccessException ex => new ApiErrorResponse
                {
                    Title = "Unauthorized",
                    Status = (int)HttpStatusCode.Unauthorized,
                    Detail = ex.Message,
                    Instance = instance,
                    TraceId = traceId
                },

                _ => new ApiErrorResponse
                {
                    Title = "Internal Server Error",
                    Status = (int)HttpStatusCode.InternalServerError,
                    Detail = "An unexpected error occurred while processing your request.",
                    Instance = instance,
                    TraceId = traceId
                }
            };
        }
    }
}
