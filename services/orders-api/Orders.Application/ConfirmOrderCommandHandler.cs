using BuildingBlocks.Common;
using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed class ConfirmOrderCommandHandler(IOrderRepository orders)
    : IRequestHandler<ConfirmOrderCommand, Result<Order>>
{
    public Task<Result<Order>> Handle(ConfirmOrderCommand command, CancellationToken cancellationToken)
    {
        var order = orders.GetById(command.OrderId);

        if (order is null)
            return Task.FromResult<Result<Order>>(OrderErrors.NotFound);

        var result = order.Confirm();

        if (result.IsSuccess)
            orders.Update(order);

        return Task.FromResult(result);
    }
}
