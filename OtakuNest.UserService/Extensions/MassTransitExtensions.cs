using MassTransit;

namespace OtakuNest.UserService.Extensions
{
    public static class MassTransitExtensions
    {
        public static IServiceCollection AddAppMassTransit(this IServiceCollection services)
        {
            services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((ctx, cfg) =>
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
    }
}
