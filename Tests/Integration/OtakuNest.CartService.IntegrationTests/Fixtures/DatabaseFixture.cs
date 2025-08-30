
using Microsoft.EntityFrameworkCore;
using OtakuNest.CartService.Data;
using Testcontainers.PostgreSql;

namespace OtakuNest.CartService.IntegrationTests.Fixtures
{
    public class DatabaseFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgresContainer;

        public string ConnectionString => _postgresContainer.GetConnectionString();
        public CartDbContext DbContext { get; private set; } = null!;
        public DatabaseFixture()
        {
            _postgresContainer = new PostgreSqlBuilder()
                .WithDatabase("ecomm-cartservice-db")
                .WithUsername("postgres")
                .WithPassword("12345678")
                .Build();
        }

        public async Task ResetDatabaseAsync()
        {
            DbContext.CartItems.RemoveRange(DbContext.CartItems);
            DbContext.Carts.RemoveRange(DbContext.Carts);
            await DbContext.SaveChangesAsync();
        }

        public async Task InitializeAsync()
        {
            await _postgresContainer.StartAsync();

            var options = new DbContextOptionsBuilder<CartDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            DbContext = new CartDbContext(options);
            await DbContext.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            await _postgresContainer.DisposeAsync();
        }
    }
}
