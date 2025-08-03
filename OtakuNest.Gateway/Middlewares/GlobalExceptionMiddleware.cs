namespace OtakuNest.Gateway.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
                _logger.LogError(ex, "Unhandled exception");
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response has already started, cannot handle exception");
                return;
            }

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
            {
                context.Response.Headers.Append("Access-Control-Allow-Origin", "http://localhost:4200");
                context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            }

            await context.Response.WriteAsJsonAsync(new
            {
                status = 500,
                message = "Something went wrong",
                detail = exception.Message // Remove in prod
            });
        }
    }
}