using Microsoft.EntityFrameworkCore;
using OtakuNest.ProductService.Data;

namespace OtakuNest.ProductService.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static async Task ApplyMigrationsAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            await dbContext.Database.MigrateAsync();
        }
    }
}
