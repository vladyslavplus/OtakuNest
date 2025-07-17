using Microsoft.EntityFrameworkCore;
using OtakuNest.OrderService.Data;

namespace OtakuNest.OrderService.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static async Task ApplyMigrationsAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            await dbContext.Database.MigrateAsync();
        }
    }
}
