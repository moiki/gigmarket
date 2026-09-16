using BuildingBlocks.Common;
using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed record GetOrdersQuery(
    int Page,
    int PageSize,
    OrderStatus? Status,
    Guid? BuyerId,
    Guid? ProviderId) : IRequest<PagedResult<Order>>;
