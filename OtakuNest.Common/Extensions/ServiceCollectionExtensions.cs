using Microsoft.Extensions.DependencyInjection;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;

namespace OtakuNest.Common.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCommonHelpers(this IServiceCollection services)
        {
            services.AddScoped(typeof(ISortHelper<>), typeof(SortHelper<>));
            return services;
        }
    }
}
