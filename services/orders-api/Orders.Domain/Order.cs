using BuildingBlocks.Common;

namespace Orders.Domain;

public sealed class Order
{
    private Order(Guid id, Guid gigId, Guid buyerId, Guid providerId, decimal price, DateTime createdAt)
    {
        Id = id;
        GigId = gigId;
        BuyerId = buyerId;
        ProviderId = providerId;
        Price = price;
        Status = OrderStatus.Created;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid GigId { get; }

    public Guid BuyerId { get; }

    public Guid ProviderId { get; }

    public decimal Price { get; }

    public OrderStatus Status { get; private set; }

    public DateTime CreatedAt { get; }

    public static Result<Order> Create(Guid id, Guid gigId, Guid buyerId, Guid providerId, decimal price, DateTime createdAt)
    {
        if (id == Guid.Empty)
            return OrderErrors.InvalidId;
        if (gigId == Guid.Empty)
            return OrderErrors.GigIdRequired;
        if (buyerId == Guid.Empty)
            return OrderErrors.BuyerRequired;
        if (providerId == Guid.Empty)
            return OrderErrors.ProviderRequired;
        if (price <= 0 || price > 100000)
            return OrderErrors.InvalidPrice;

        return new Order(id, gigId, buyerId, providerId, price, createdAt);
    }

    public Result<Order> Confirm()
    {
        if (Status != OrderStatus.Created)
            return OrderErrors.InvalidStatusTransition;

        Status = OrderStatus.Confirmed;
        return this;
    }

    public Result<Order> Cancel()
    {
        if (Status == OrderStatus.Cancelled)
            return OrderErrors.InvalidStatusTransition;

        Status = OrderStatus.Cancelled;
        return this;
    }
}