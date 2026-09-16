using Orders.Application;
using Orders.Domain;
using Orders.Infrastructure;

namespace Orders.UnitTests;

public class GetOrdersQueryHandlerTests
{
    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid OtherBuyerId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();
    private static readonly Guid OtherProviderId = Guid.NewGuid();

    private static readonly DateTime T0 = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc);

    private static Order NewOrder(Guid buyerId, Guid providerId, DateTime createdAt, OrderStatus status = OrderStatus.Created)
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), buyerId, providerId, 25m, createdAt).Value;

        if (status == OrderStatus.Confirmed)
            order.Confirm();
        else if (status == OrderStatus.Cancelled)
            order.Cancel();

        return order;
    }

    private static InMemoryOrderRepository NewRepository(params Order[] orders)
    {
        var repository = new InMemoryOrderRepository();

        foreach (var order in orders)
            repository.Add(order);

        return repository;
    }

    [Fact]
    public async Task Handle_ReturnsOrdersOrderedByCreatedAtDescending()
    {
        var oldest = NewOrder(BuyerId, ProviderId, T0);
        var newest = NewOrder(BuyerId, ProviderId, T2);
        var middle = NewOrder(BuyerId, ProviderId, T1);
        var handler = new GetOrdersQueryHandler(NewRepository(oldest, newest, middle));

        var result = await handler.Handle(new GetOrdersQuery(1, 20, null, null, null), CancellationToken.None);

        Assert.Equal(new[] { newest.Id, middle.Id, oldest.Id }, result.Items.Select(o => o.Id));
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsRequestedPageAndTotalPages()
    {
        var orders = Enumerable.Range(0, 5)
            .Select(i => NewOrder(BuyerId, ProviderId, T0.AddMinutes(i)))
            .ToArray();
        var handler = new GetOrdersQueryHandler(NewRepository(orders));

        var result = await handler.Handle(new GetOrdersQuery(2, 2, null, null, null), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ReturnsOnlyMatchingOrders()
    {
        var created = NewOrder(BuyerId, ProviderId, T0);
        var confirmed = NewOrder(BuyerId, ProviderId, T1, OrderStatus.Confirmed);
        var cancelled = NewOrder(BuyerId, ProviderId, T2, OrderStatus.Cancelled);
        var handler = new GetOrdersQueryHandler(NewRepository(created, confirmed, cancelled));

        var result = await handler.Handle(new GetOrdersQuery(1, 20, OrderStatus.Confirmed, null, null), CancellationToken.None);

        Assert.Equal(confirmed.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Handle_WithBuyerFilter_ReturnsOnlyThatBuyersOrders()
    {
        var mine = NewOrder(BuyerId, ProviderId, T0);
        var other = NewOrder(OtherBuyerId, ProviderId, T1);
        var handler = new GetOrdersQueryHandler(NewRepository(mine, other));

        var result = await handler.Handle(new GetOrdersQuery(1, 20, null, BuyerId, null), CancellationToken.None);

        Assert.Equal(mine.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Handle_WithProviderFilter_ReturnsOnlyThatProvidersOrders()
    {
        var mine = NewOrder(BuyerId, ProviderId, T0);
        var other = NewOrder(BuyerId, OtherProviderId, T1);
        var handler = new GetOrdersQueryHandler(NewRepository(mine, other));

        var result = await handler.Handle(new GetOrdersQuery(1, 20, null, null, ProviderId), CancellationToken.None);

        Assert.Equal(mine.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Handle_WithoutMatches_ReturnsEmptyPage()
    {
        var handler = new GetOrdersQueryHandler(NewRepository(NewOrder(BuyerId, ProviderId, T0)));

        var result = await handler.Handle(new GetOrdersQuery(1, 20, OrderStatus.Cancelled, null, null), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }
}
