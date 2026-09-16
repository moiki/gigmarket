using BuildingBlocks.Common;
using Microsoft.EntityFrameworkCore;
using Orders.Application;
using Orders.Domain;

namespace Orders.Infrastructure;

public sealed class EfOrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public void Add(Order order)
    {
        dbContext.Orders.Add(order);
        dbContext.SaveChanges();
    }

    public void Update(Order order)
    {
        dbContext.Orders.Update(order);
        dbContext.SaveChanges();
    }

    public Order? GetById(Guid id) => dbContext.Orders.Find(id);

    public PagedResult<Order> GetOrders(OrderStatus? status, Guid? buyerId, Guid? providerId, int page, int pageSize)
    {
        var query = dbContext.Orders.AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (buyerId.HasValue)
            query = query.Where(o => o.BuyerId == buyerId.Value);

        if (providerId.HasValue)
            query = query.Where(o => o.ProviderId == providerId.Value);

        query = query.OrderByDescending(o => o.CreatedAt);

        var totalCount = query.Count();
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<Order>(items, page, pageSize, totalCount, totalPages);
    }
}
