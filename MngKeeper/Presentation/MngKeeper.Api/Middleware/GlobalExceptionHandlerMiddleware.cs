using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Api.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
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
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var (statusCode, message) = exception switch
            {
                ArgumentNullException => (HttpStatusCode.BadRequest, "Required argument is missing"),
                ArgumentException => (HttpStatusCode.BadRequest, "Invalid argument provided"),
                System.UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Access denied"),
                InvalidOperationException => (HttpStatusCode.BadRequest, "Invalid operation"),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
                TimeoutException => (HttpStatusCode.RequestTimeout, "Operation timed out"),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
            };

            context.Response.StatusCode = (int)statusCode;

            var response = new
            {
                StatusCode = statusCode,
                Message = message,
                Timestamp = DateTime.UtcNow,
                TraceId = context.TraceIdentifier,
                Path = context.Request.Path,
                Method = context.Request.Method
            };

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
        }
    }

    public static class GlobalExceptionHandlerMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        }
    }
}
