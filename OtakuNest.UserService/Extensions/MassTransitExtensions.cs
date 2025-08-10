using MassTransit;
using OtakuNest.UserService.Consumers;

namespace OtakuNest.UserService.Extensions
{
    public static class MassTransitExtensions
    {
        public static IServiceCollection AddAppMassTransit(this IServiceCollection services)
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<GetUsersByIdsConsumer>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host("rabbitmq", "/", h =>
                    {
                        h.Username("guest");
                        h.Password("guest");
                    });

                    cfg.ReceiveEndpoint("get-users-by-ids-queue", e =>
                    {
                        e.ConfigureConsumer<GetUsersByIdsConsumer>(context);
                    });
                });
            });

            return services;
        }
    }
}
