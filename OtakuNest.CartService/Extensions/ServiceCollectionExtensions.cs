using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.CartService.Consumers;
using OtakuNest.CartService.Data;
using OtakuNest.CartService.Services;
using OtakuNest.Contracts;

namespace OtakuNest.CartService.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<CartDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            return services;
        }

        public static IServiceCollection AddRabbitMq(this IServiceCollection services)
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<UserCreatedConsumer>();
                x.AddRequestClient<CheckProductQuantityRequest>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host("rabbitmq", "/", h =>
                    {
                        h.Username("guest");
                        h.Password("guest");
                    });

                    cfg.ReceiveEndpoint("user-created-cart", e =>
                    {
                        e.ConfigureConsumer<UserCreatedConsumer>(context);
                    });
                });
            });

            return services;
        }


        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            services.AddScoped<ICartService, Services.CartService>();
            return services;
        }
    }
}
