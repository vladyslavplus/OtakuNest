using MassTransit;
using MassTransit.Testing;
using OtakuNest.Contracts;

namespace OtakuNest.CartService.IntegrationTests.Fixtures
{
    public class MassTransitFixture : IAsyncLifetime
    {
        public InMemoryTestHarness Harness { get; private set; } = null!;
        public IRequestClient<CheckProductQuantityRequest> QuantityClient { get; private set; } = null!;
        public async Task InitializeAsync()
        {
            Harness = new InMemoryTestHarness();

            Harness.OnConfigureInMemoryBus += configurator =>
            {
                configurator.ReceiveEndpoint("check-product-quantity-queue", e =>
                {
                    e.Handler<CheckProductQuantityRequest>(async context =>
                    {
                        await context.RespondAsync(new CheckProductQuantityResponse(
                            context.Message.ProductId,
                            10
                        ));
                    });
                });
            };

            await Harness.Start();

            QuantityClient = Harness.CreateRequestClient<CheckProductQuantityRequest>(
                new Uri("queue:check-product-quantity-queue")
            );
        }

        public async Task DisposeAsync()
        {
            await Harness.Stop();
        }
    }
}
