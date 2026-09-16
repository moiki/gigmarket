using BuildingBlocks.Common;
using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed class CancelOrderCommandHandler(IOrderRepository orders)
    : IRequestHandler<CancelOrderCommand, Result<Order>>
{
    public Task<Result<Order>> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = orders.GetById(command.OrderId);

        if (order is null)
            return Task.FromResult<Result<Order>>(OrderErrors.NotFound);

        var result = order.Cancel();

        if (result.IsSuccess)
            orders.Update(order);

        return Task.FromResult(result);
    }
}
