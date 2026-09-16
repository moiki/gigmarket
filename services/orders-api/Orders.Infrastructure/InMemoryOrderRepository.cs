using BuildingBlocks.Common;
using Orders.Application;
using Orders.Domain;

namespace Orders.Infrastructure;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = [];

    public void Add(Order order) => _orders[order.Id] = order;

    public void Update(Order order) => _orders[order.Id] = order;

    public Order? GetById(Guid id) => _orders.GetValueOrDefault(id);

    public IReadOnlyCollection<Order> GetAll() => _orders.Values.ToList();

    public PagedResult<Order> GetOrders(OrderStatus? status, Guid? buyerId, Guid? providerId, int page, int pageSize)
    {
        var matching = _orders.Values
            .Where(o => !status.HasValue || o.Status == status.Value)
            .Where(o => !buyerId.HasValue || o.BuyerId == buyerId.Value)
            .Where(o => !providerId.HasValue || o.ProviderId == providerId.Value)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        var items = matching
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling(matching.Count / (double)pageSize);

        return new PagedResult<Order>(items, page, pageSize, matching.Count, totalPages);
    }
}
