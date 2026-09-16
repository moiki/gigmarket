using Microsoft.EntityFrameworkCore;
using Orders.Domain;
using Orders.Infrastructure;
using Testcontainers.PostgreSql;

namespace Orders.IntegrationTests;

public class EfOrderRepositoryTests
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("gigmarket_orders_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private async Task<OrderDbContext> NewDbContext()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        var context = new OrderDbContext(options);
        await context.Database.MigrateAsync();
        return context;
    }

    [Fact]
    public async Task AddAndGetById_PersistsOrder()
    {
        var dbContext = await NewDbContext();

        var order = Order.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            25m,
            new DateTime(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc)).Value;

        var repository = new EfOrderRepository(dbContext);
        repository.Add(order);

        var loaded = repository.GetById(order.Id);

        Assert.NotNull(loaded);
        Assert.Equal(order.GigId, loaded.GigId);
        Assert.Equal(order.BuyerId, loaded.BuyerId);
        Assert.Equal(order.ProviderId, loaded.ProviderId);
        Assert.Equal(order.Price, loaded.Price);
        Assert.Equal(OrderStatus.Created, loaded.Status);
        Assert.Equal(order.CreatedAt, loaded.CreatedAt);

        await dbContext.DisposeAsync();
    }
}