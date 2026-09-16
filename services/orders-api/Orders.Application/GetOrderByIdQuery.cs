using MediatR;
using Orders.Domain;

namespace Orders.Application;

public sealed record GetOrderByIdQuery(Guid OrderId) : IRequest<Order?>;
