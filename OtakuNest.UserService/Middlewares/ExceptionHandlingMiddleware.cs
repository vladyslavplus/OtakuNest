using System.Net;
using System.Text.Json;

namespace OtakuNest.UserService.Middlewares
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

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred.");

                context.Response.ContentType = "application/json";

                context.Response.StatusCode = ex switch
                {
                    ApplicationException => (int)HttpStatusCode.BadRequest,
                    InvalidOperationException => (int)HttpStatusCode.Conflict,
                    UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                    KeyNotFoundException => (int)HttpStatusCode.NotFound,
                    _ => (int)HttpStatusCode.InternalServerError
                };

                var result = JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    statusCode = context.Response.StatusCode
                });

                await context.Response.WriteAsync(result);
            }
        }
    }
}
