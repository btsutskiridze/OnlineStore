using Orders.Api.Responses;
using Orders.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace Orders.Api.Middleware
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
                OrderNotFoundException ex => new ApiErrorResponse
                {
                    Title = "Order Not Found",
                    Status = (int)HttpStatusCode.NotFound,
                    Detail = ex.Message,
                    Instance = instance,
                    TraceId = traceId
                },

                InsufficientStockException ex => new ApiErrorResponse
                {
                    Title = "Insufficient Stock",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = ex.Message,
                    Instance = instance,
                    TraceId = traceId
                },

                OrdersException ex => new ApiErrorResponse
                {
                    Title = "Orders Error",
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
