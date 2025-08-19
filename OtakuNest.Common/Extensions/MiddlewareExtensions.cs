using Microsoft.AspNetCore.Builder;
using OtakuNest.Common.Middlewares;

namespace OtakuNest.Common.Extensions
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        }
    }
}
