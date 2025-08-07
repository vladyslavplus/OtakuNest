using Microsoft.EntityFrameworkCore;
using OtakuNest.CommentService.Data;

namespace OtakuNest.CommentService.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static async Task ApplyMigrationsAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CommentDbContext>();
            await dbContext.Database.MigrateAsync();
        }
    }
}
