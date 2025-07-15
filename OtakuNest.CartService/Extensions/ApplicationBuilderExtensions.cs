using Microsoft.EntityFrameworkCore;
using OtakuNest.CartService.Data;

namespace OtakuNest.CartService.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static async Task ApplyMigrationsAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<CartDbContext>();
            await dbContext.Database.MigrateAsync();
        }
    }
}
