using System.Net;
using Microsoft.AspNetCore.Mvc;
using OtakuNest.CartService.Exceptions;

namespace OtakuNest.CartService.Middlewares
{
    public class CartExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CartExceptionHandlingMiddleware> _logger;

        public CartExceptionHandlingMiddleware(RequestDelegate next, ILogger<CartExceptionHandlingMiddleware> logger)
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
            catch (NotEnoughStockException ex)
            {
                _logger.LogWarning(ex,
                    "Not enough stock for product {ProductId}. Requested {Requested}, available {Available}.",
                    ex.ProductId, ex.Requested, ex.Available);

                var problemDetails = new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Not enough stock",
                    Detail = ex.Message,
                    Type = "https://httpstatuses.com/400"
                };

                problemDetails.Extensions["productId"] = ex.ProductId;
                problemDetails.Extensions["requested"] = ex.Requested;
                problemDetails.Extensions["available"] = ex.Available;

                context.Response.StatusCode = problemDetails.Status.Value;
                context.Response.ContentType = "application/problem+json";

                await context.Response.WriteAsJsonAsync(problemDetails);
            }
        }
    }
}