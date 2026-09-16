using Orders.Domain;

namespace Orders.UnitTests;

public class OrderTransitionTests
{
    private static readonly DateTime CreatedAt = new(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);

    private static Order NewOrder() =>
        Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25m, CreatedAt).Value;

    [Fact]
    public void Confirm_FromCreated_MovesToConfirmed()
    {
        var order = NewOrder();

        var result = order.Confirm();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void Confirm_FromConfirmed_ReturnsInvalidStatusTransition()
    {
        var order = NewOrder();
        order.Confirm();

        var result = order.Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition.Code, result.Error?.Code);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void Confirm_FromCancelled_ReturnsInvalidStatusTransition()
    {
        var order = NewOrder();
        order.Cancel();

        var result = order.Confirm();

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition.Code, result.Error?.Code);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_FromCreated_MovesToCancelled()
    {
        var order = NewOrder();

        var result = order.Cancel();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_FromConfirmed_MovesToCancelled()
    {
        var order = NewOrder();
        order.Confirm();

        var result = order.Cancel();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_FromCancelled_ReturnsInvalidStatusTransition()
    {
        var order = NewOrder();
        order.Cancel();

        var result = order.Cancel();

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition.Code, result.Error?.Code);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }
}
