using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OtakuNest.Common.Helpers;
using OtakuNest.Common.Interfaces;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

namespace OtakuNest.Common.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCommonHelpers(this IServiceCollection services)
        {
            services.AddScoped(typeof(ISortHelper<>), typeof(SortHelper<>));
            return services;
        }

        public static IServiceCollection AddFluentValidationSetup(this IServiceCollection services, Assembly assembly)
        {
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

            return services;
        }
    }
}
