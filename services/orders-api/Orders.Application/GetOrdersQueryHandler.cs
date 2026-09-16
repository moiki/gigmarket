using BuildingBlocks.Common;
using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed class GetOrdersQueryHandler(IOrderRepository orders)
    : IRequestHandler<GetOrdersQuery, PagedResult<Order>>
{
    public Task<PagedResult<Order>> Handle(GetOrdersQuery query, CancellationToken cancellationToken)
        => Task.FromResult(orders.GetOrders(query.Status, query.BuyerId, query.ProviderId, query.Page, query.PageSize));
}
