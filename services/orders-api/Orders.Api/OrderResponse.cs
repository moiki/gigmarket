using Orders.Domain;

namespace Orders.Api;

public sealed record OrderResponse(
    Guid Id,
    Guid GigId,
    Guid BuyerId,
    Guid ProviderId,
    decimal Price,
    OrderStatus Status,
    DateTime CreatedAt)
{
    public static OrderResponse From(Order order) =>
        new(order.Id, order.GigId, order.BuyerId, order.ProviderId, order.Price, order.Status, order.CreatedAt);
}