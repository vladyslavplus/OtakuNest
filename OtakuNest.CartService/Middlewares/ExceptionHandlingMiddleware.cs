using System.Net;
using System.Text.Json;
using OtakuNest.CartService.Exceptions;

namespace OtakuNest.CartService.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (NotEnoughStockException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";

                var result = JsonSerializer.Serialize(new
                {
                    error = "NotEnoughStock",
                    productId = ex.ProductId,
                    requested = ex.Requested,
                    available = ex.Available,
                    message = ex.Message
                });

                await context.Response.WriteAsync(result);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var result = JsonSerializer.Serialize(new
                {
                    error = "InternalServerError",
                    message = ex.Message
                });

                await context.Response.WriteAsync(result);
            }
        }
    }
}
