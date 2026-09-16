using BuildingBlocks.Common;
using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed class CreateOrderCommandHandler(
    IGigCatalog catalog,
    IOrderRepository orders,
    TimeProvider clock)
    : IRequestHandler<CreateOrderCommand, Result<Order>>
{
    public async Task<Result<Order>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var gig = await catalog.GetByIdAsync(command.GigId, cancellationToken);

        if (gig.IsFailure)
            return gig.Error.Value;

        if (!gig.Value.IsActive)
            return OrderErrors.GigNotActive;

        var order = Order.Create(
            Guid.NewGuid(),
            command.GigId,
            command.BuyerId,
            gig.Value.OwnerId,
            gig.Value.Price,
            clock.GetUtcNow().UtcDateTime);

        if (order.IsFailure)
            return order;

        orders.Add(order.Value);
        return order;
    }
}