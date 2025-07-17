using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OtakuNest.OrderService.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
                _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = context.Response;
            response.ContentType = "application/json";

            var (statusCode, message) = exception switch
            {
                ArgumentNullException or ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Access denied."),
                InvalidOperationException => (HttpStatusCode.Conflict, exception.Message),
                KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
                DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Concurrency conflict."),
                DbUpdateException => (HttpStatusCode.BadRequest, "Database update failed."),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
            };

            var problemDetails = new
            {
                status = (int)statusCode,
                title = "An error occurred while processing your request.",
                detail = message,
                timestamp = DateTime.UtcNow
            };

            response.StatusCode = (int)statusCode;
            await response.WriteAsync(JsonSerializer.Serialize(problemDetails));
        }
    }
}
