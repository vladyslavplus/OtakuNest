using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.Contracts;
using OtakuNest.OrderService.Data;
using OtakuNest.OrderService.Services;

namespace OtakuNest.OrderService.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<OrdersDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            return services;
        }

        public static IServiceCollection AddRabbitMq(this IServiceCollection services)
        {
            services.AddMassTransit(x =>
            {
                x.AddRequestClient<CheckProductPriceRequest>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host("rabbitmq", "/", h =>
                    {
                        h.Username("guest");
                        h.Password("guest");
                    });
                });
            });

            return services;
        }


        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            services.AddScoped<IOrderService, Services.OrderService>();
            return services;
        }
    }
}
