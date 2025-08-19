using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OtakuNest.Common.Middlewares
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

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, _logger);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception ex, ILogger logger)
        {
            HttpStatusCode statusCode;
            string errorType;

            switch (ex)
            {
                case ArgumentNullException:
                case ArgumentException:
                    statusCode = HttpStatusCode.BadRequest;
                    errorType = "BadRequest";
                    break;

                case KeyNotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    errorType = "NotFound";
                    break;

                case UnauthorizedAccessException:
                    statusCode = HttpStatusCode.Unauthorized;
                    errorType = "Unauthorized";
                    break;

                case InvalidOperationException:
                    statusCode = HttpStatusCode.Conflict;
                    errorType = "Conflict";
                    break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    errorType = "InternalServerError";
                    break;
            }

            logger.LogError(ex, "Unhandled exception: {ErrorType}", errorType);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var response = new
            {
                error = errorType,
                message = ex.Message,
#if DEBUG
                details = ex.StackTrace 
#endif
            };

            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await context.Response.WriteAsync(json);
        }
    }
}
