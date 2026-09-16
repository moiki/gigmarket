namespace Orders.Infrastructure;

public sealed class IdempotencyRecord
{
    public long Id { get; set; }

    public Guid BuyerId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}