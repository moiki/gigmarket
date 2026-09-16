using BuildingBlocks.Common;
using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed record CreateOrderCommand(Guid GigId, Guid BuyerId) : IRequest<Result<Order>>;