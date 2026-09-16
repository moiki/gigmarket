using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed class GetOrderByIdQueryHandler(IOrderRepository orders)
    : IRequestHandler<GetOrderByIdQuery, Order?>
{
    public Task<Order?> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
        => Task.FromResult(orders.GetById(query.OrderId));
}
