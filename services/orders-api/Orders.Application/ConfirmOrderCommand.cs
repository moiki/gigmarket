using BuildingBlocks.Common;
using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed record ConfirmOrderCommand(Guid OrderId) : IRequest<Result<Order>>;
