using BuildingBlocks.Common;
using Orders.Domain;

namespace Orders.Application;

public interface IOrderRepository
{
    void Add(Order order);

    void Update(Order order);

    Order? GetById(Guid id);

    PagedResult<Order> GetOrders(OrderStatus? status, Guid? buyerId, Guid? providerId, int page, int pageSize);
}
