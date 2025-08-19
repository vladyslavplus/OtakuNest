using System.Net;
using Microsoft.AspNetCore.Mvc;
using OtakuNest.UserService.Exceptions;

namespace OtakuNest.UserService.Middlewares
{
    public class UserExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UserExceptionHandlingMiddleware> _logger;

        public UserExceptionHandlingMiddleware(RequestDelegate next, ILogger<UserExceptionHandlingMiddleware> logger)
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
            catch (RoleAssignmentException ex)
            {
                _logger.LogError(ex, "Role assignment failed: {Message}", ex.Message);

                var problemDetails = new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "Role assignment error",
                    Detail = ex.Message,
                    Type = "https://httpstatuses.com/400"
                };

                context.Response.StatusCode = problemDetails.Status.Value;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problemDetails);
            }
            catch (UserCreationException ex)
            {
                _logger.LogError(ex, "User creation failed: {Message}", ex.Message);

                var problemDetails = new ProblemDetails
                {
                    Status = (int)HttpStatusCode.BadRequest,
                    Title = "User creation error",
                    Detail = ex.Message,
                    Type = "https://httpstatuses.com/400"
                };

                context.Response.StatusCode = problemDetails.Status.Value;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problemDetails);
            }
        }
    }
}
