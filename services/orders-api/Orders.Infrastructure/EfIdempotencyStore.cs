using Microsoft.EntityFrameworkCore;
using Orders.Application;

namespace Orders.Infrastructure;

public sealed class EfIdempotencyStore(OrderDbContext dbContext, TimeProvider clock) : IIdempotencyStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public IdempotencyEntry? TryGet(Guid buyerId, string key)
    {
        var record = dbContext.IdempotencyRecords.SingleOrDefault(r => r.BuyerId == buyerId && r.Key == key);

        if (record is null)
            return null;

        if (record.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
        {
            dbContext.IdempotencyRecords.Remove(record);
            dbContext.SaveChanges();
            return null;
        }

        return new IdempotencyEntry(record.OrderId, record.RequestHash);
    }

    public void Record(Guid buyerId, string key, string requestHash, Guid orderId, DateTime now)
    {
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            BuyerId = buyerId,
            Key = key,
            RequestHash = requestHash,
            OrderId = orderId,
            CreatedAt = now,
            ExpiresAt = now + Ttl
        });

        dbContext.SaveChanges();
    }
}