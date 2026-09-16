using Microsoft.EntityFrameworkCore;
using Orders.Domain;

namespace Orders.Infrastructure;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<Order>();
        order.ToTable("orders");
        order.HasKey(o => o.Id);
        order.Property(o => o.GigId);
        order.Property(o => o.BuyerId);
        order.Property(o => o.ProviderId);
        order.Property(o => o.Price).HasPrecision(18, 2);
        order.Property(o => o.Status).HasConversion<string>().HasMaxLength(10);
        order.Property(o => o.CreatedAt);

        var idempotency = modelBuilder.Entity<IdempotencyRecord>();
        idempotency.ToTable("idempotency_records");
        idempotency.HasKey(i => i.Id);
        idempotency.Property(i => i.Id).ValueGeneratedOnAdd();
        idempotency.Property(i => i.BuyerId);
        idempotency.Property(i => i.Key).HasMaxLength(128);
        idempotency.Property(i => i.RequestHash).HasMaxLength(64);
        idempotency.Property(i => i.OrderId);
        idempotency.Property(i => i.CreatedAt);
        idempotency.Property(i => i.ExpiresAt);
        idempotency.HasIndex(i => new { i.BuyerId, i.Key }).IsUnique();
    }
}