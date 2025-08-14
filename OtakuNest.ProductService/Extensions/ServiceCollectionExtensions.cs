using MassTransit;
using Microsoft.EntityFrameworkCore;
using OtakuNest.ProductService.Consumers;
using OtakuNest.ProductService.Data;
using OtakuNest.ProductService.Services;

namespace OtakuNest.ProductService.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAppDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ProductDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            return services;
        }

        public static IServiceCollection AddRabbitMq(this IServiceCollection services)
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<CheckProductQuantityConsumer>();
                x.AddConsumer<CheckProductPriceConsumer>();
                x.AddConsumer<ProductQuantityUpdatedConsumer>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host("rabbitmq", "/", h =>
                    {
                        h.Username("guest");
                        h.Password("guest");
                    });

                    cfg.ReceiveEndpoint("check-product-quantity-queue", e =>
                    {
                        e.ConfigureConsumer<CheckProductQuantityConsumer>(context);
                    });

                    cfg.ReceiveEndpoint("product-service-price-check", e =>
                    {
                        e.ConfigureConsumer<CheckProductPriceConsumer>(context);
                    });

                    cfg.ReceiveEndpoint("product-quantity-updated-queue", e =>
                    {
                        e.ConfigureConsumer<ProductQuantityUpdatedConsumer>(context);
                    });
                });
            });

            return services;
        }

        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            services.AddScoped<IProductService, Services.ProductService>();
            services.AddScoped<ProductSeeder>();
            return services;
        }
    }
}
