namespace OtakuNest.Gateway.Middlewares
{
    public class RateLimitCorsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitCorsMiddleware> _logger;

        public RateLimitCorsMiddleware(RequestDelegate next, ILogger<RateLimitCorsMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (context.Response.StatusCode == 429)
            {
                _logger.LogWarning("Rate limit response detected, ensuring CORS headers are present");

                if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
                {
                    context.Response.Headers.Append("Access-Control-Allow-Origin", "http://localhost:4200");
                }

                if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Credentials"))
                {
                    context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
                }

                if (string.IsNullOrEmpty(context.Response.ContentType))
                {
                    context.Response.ContentType = "application/json";
                }

                if (context.Response.ContentLength == 0 || context.Response.ContentLength == null)
                {
                    var responseBody = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "Too Many Requests",
                        message = "Rate limit exceeded. Please try again later.",
                        retryAfter = 30
                    });

                    await context.Response.WriteAsync(responseBody);
                }
            }
        }
    }
}
