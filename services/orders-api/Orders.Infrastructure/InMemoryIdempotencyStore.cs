using Orders.Application;

namespace Orders.Infrastructure;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly Dictionary<(Guid BuyerId, string Key), (string RequestHash, Guid OrderId, DateTime CreatedAt)> _records = [];

    public IdempotencyEntry? TryGet(Guid buyerId, string key)
    {
        if (!_records.TryGetValue((buyerId, key), out var record))
            return null;

        if (record.CreatedAt + Ttl <= DateTime.UtcNow)
        {
            _records.Remove((buyerId, key));
            return null;
        }

        return new IdempotencyEntry(record.OrderId, record.RequestHash);
    }

    public void Record(Guid buyerId, string key, string requestHash, Guid orderId, DateTime now)
        => _records[(buyerId, key)] = (requestHash, orderId, now);
}