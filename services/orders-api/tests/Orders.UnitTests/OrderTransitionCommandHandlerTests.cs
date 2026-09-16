using Orders.Application;
using Orders.Domain;
using Orders.Infrastructure;

namespace Orders.UnitTests;

public class OrderTransitionCommandHandlerTests
{
    private static readonly DateTime CreatedAt = new(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);

    private static Order NewOrder() =>
        Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25m, CreatedAt).Value;

    [Fact]
    public async Task Confirm_WithExistingOrder_ConfirmsAndPersists()
    {
        var repository = new InMemoryOrderRepository();
        var order = NewOrder();
        repository.Add(order);
        var handler = new ConfirmOrderCommandHandler(repository);

        var result = await handler.Handle(new ConfirmOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, result.Value.Status);
        Assert.Equal(OrderStatus.Confirmed, repository.GetById(order.Id)!.Status);
    }

    [Fact]
    public async Task Confirm_WithUnknownOrder_ReturnsNotFound()
    {
        var handler = new ConfirmOrderCommandHandler(new InMemoryOrderRepository());

        var result = await handler.Handle(new ConfirmOrderCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error?.Code);
    }

    [Fact]
    public async Task Confirm_WithAlreadyConfirmedOrder_ReturnsInvalidStatusTransition()
    {
        var repository = new InMemoryOrderRepository();
        var order = NewOrder();
        order.Confirm();
        repository.Add(order);
        var handler = new ConfirmOrderCommandHandler(repository);

        var result = await handler.Handle(new ConfirmOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition.Code, result.Error?.Code);
    }

    [Fact]
    public async Task Cancel_WithCreatedOrder_CancelsAndPersists()
    {
        var repository = new InMemoryOrderRepository();
        var order = NewOrder();
        repository.Add(order);
        var handler = new CancelOrderCommandHandler(repository);

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, result.Value.Status);
        Assert.Equal(OrderStatus.Cancelled, repository.GetById(order.Id)!.Status);
    }

    [Fact]
    public async Task Cancel_WithUnknownOrder_ReturnsNotFound()
    {
        var handler = new CancelOrderCommandHandler(new InMemoryOrderRepository());

        var result = await handler.Handle(new CancelOrderCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error?.Code);
    }

    [Fact]
    public async Task Cancel_WithCancelledOrder_ReturnsInvalidStatusTransition()
    {
        var repository = new InMemoryOrderRepository();
        var order = NewOrder();
        order.Cancel();
        repository.Add(order);
        var handler = new CancelOrderCommandHandler(repository);

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition.Code, result.Error?.Code);
    }
}
