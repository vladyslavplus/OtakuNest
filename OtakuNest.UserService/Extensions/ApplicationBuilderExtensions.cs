using Microsoft.AspNetCore.Identity;
using OtakuNest.UserService.Data;
using OtakuNest.UserService.Models;

namespace OtakuNest.UserService.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static async Task SeedDatabaseAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            await DbInitializer.SeedAsync(context, userManager, roleManager);
        }
    }
}
