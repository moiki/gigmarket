namespace Orders.Api;

public sealed record CreateOrderRequest(Guid GigId, Guid BuyerId);