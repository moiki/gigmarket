using BuildingBlocks.Common;
using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed record CancelOrderCommand(Guid OrderId) : IRequest<Result<Order>>;
