using Microsoft.EntityFrameworkCore;
using Orders.Domain;
using Orders.Infrastructure;
using Testcontainers.PostgreSql;

namespace Orders.IntegrationTests;

public class EfOrderRepositoryQueriesTests
{
    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid OtherBuyerId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("gigmarket_orders_queries_test")
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

    private static Order NewOrder(Guid buyerId, DateTime createdAt, OrderStatus status = OrderStatus.Created)
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), buyerId, ProviderId, 25m, createdAt).Value;

        if (status == OrderStatus.Confirmed)
            order.Confirm();

        return order;
    }

    [Fact]
    public async Task GetOrders_OrdersByCreatedAtDescendingAndPaginates()
    {
        var dbContext = await NewDbContext();
        var repository = new EfOrderRepository(dbContext);

        var oldest = NewOrder(BuyerId, T0);
        var newest = NewOrder(BuyerId, T0.AddHours(2));
        var middle = NewOrder(BuyerId, T0.AddHours(1));
        repository.Add(oldest);
        repository.Add(newest);
        repository.Add(middle);

        var page = repository.GetOrders(null, null, null, 1, 2);

        Assert.Equal(new[] { newest.Id, middle.Id }, page.Items.Select(o => o.Id));
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);

        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetOrders_FiltersByStatusAndBuyer()
    {
        var dbContext = await NewDbContext();
        var repository = new EfOrderRepository(dbContext);

        var confirmed = NewOrder(BuyerId, T0, OrderStatus.Confirmed);
        repository.Add(confirmed);
        repository.Add(NewOrder(BuyerId, T0.AddHours(1)));
        repository.Add(NewOrder(OtherBuyerId, T0.AddHours(2), OrderStatus.Confirmed));

        var byStatus = repository.GetOrders(OrderStatus.Confirmed, null, null, 1, 20);
        var byBuyerAndStatus = repository.GetOrders(OrderStatus.Confirmed, BuyerId, null, 1, 20);

        Assert.Equal(2, byStatus.TotalCount);
        Assert.Equal(confirmed.Id, Assert.Single(byBuyerAndStatus.Items).Id);

        await dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Update_PersistsStatusTransition()
    {
        var dbContext = await NewDbContext();
        var repository = new EfOrderRepository(dbContext);

        var order = NewOrder(BuyerId, T0);
        repository.Add(order);

        order.Confirm();
        repository.Update(order);

        dbContext.ChangeTracker.Clear();
        var reloaded = repository.GetById(order.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(OrderStatus.Confirmed, reloaded.Status);

        await dbContext.DisposeAsync();
    }
}
