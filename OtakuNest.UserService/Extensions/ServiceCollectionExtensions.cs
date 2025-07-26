using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;
using OtakuNest.UserService.Models;
using OtakuNest.UserService.Services;

namespace OtakuNest.UserService.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserService, Services.UserService>();
            services.AddScoped<ISortHelper<ApplicationUser>, SortHelper<ApplicationUser>>();
            services.AddScoped<ITokenService, TokenService>();


            return services;
        }
    }
}
