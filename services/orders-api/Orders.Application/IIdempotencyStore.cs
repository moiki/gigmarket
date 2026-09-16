namespace Orders.Application;

public sealed record IdempotencyEntry(Guid OrderId, string RequestHash);

public interface IIdempotencyStore
{
    IdempotencyEntry? TryGet(Guid buyerId, string key);

    void Record(Guid buyerId, string key, string requestHash, Guid orderId, DateTime now);
}