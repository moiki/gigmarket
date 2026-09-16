using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api;
using Orders.Application;
using Orders.Infrastructure;
using Testcontainers.PostgreSql;

namespace Orders.IntegrationTests;

public class CreateOrderIdempotencyIntegrationTests
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("gigmarket_orders_idem_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Guid BuyerId = Guid.NewGuid();

    private static HttpRequestMessage NewOrderRequest(Guid gigId, Guid buyerId, string? idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { gigId, buyerId })
        };

        if (idempotencyKey is not null)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        return request;
    }

    private async Task<(OrdersApiFactory Factory, Guid GigId)> NewFactory()
    {
        await _postgres.StartAsync();

        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, Guid.NewGuid(), true)));

        var factory = new OrdersApiFactory(_postgres.GetConnectionString(), catalog);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        return (factory, gigId);
    }

    private static async Task<int> CountOrdersAsync(OrdersApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        return await dbContext.Orders.CountAsync();
    }

    [Fact]
    public async Task PostOrder_WithSameKey_ReplaysSingleOrder()
    {
        var (factory, gigId) = await NewFactory();
        using var api = factory;
        var client = api.CreateClient();
        const string key = "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d";

        var first = await client.SendAsync(NewOrderRequest(gigId, BuyerId, key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);

        var replay = await client.SendAsync(NewOrderRequest(gigId, BuyerId, key));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        var replayed = await replay.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);

        Assert.Equal(created!.Id, replayed!.Id);
        Assert.Equal(1, await CountOrdersAsync(factory));
    }

    [Fact]
    public async Task PostOrder_ConcurrentSameKey_CreatesSingleOrder()
    {
        var (factory, gigId) = await NewFactory();
        using var api = factory;
        var client = api.CreateClient();
        const string key = "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d";

        var first = client.SendAsync(NewOrderRequest(gigId, BuyerId, key));
        var second = client.SendAsync(NewOrderRequest(gigId, BuyerId, key));
        var responses = await Task.WhenAll(first, second);

        foreach (var response in responses)
        {
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        }

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, await CountOrdersAsync(factory));
    }

    [Fact]
    public async Task TryGet_OnExpiredRecord_ReturnsNullAndRemovesIt()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        var dbContext = new OrderDbContext(options);
        await dbContext.Database.MigrateAsync();

        var buyerId = Guid.NewGuid();
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord
        {
            BuyerId = buyerId,
            Key = "expired-key",
            RequestHash = "hash",
            OrderId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddHours(-25),
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await dbContext.SaveChangesAsync();

        var store = new EfIdempotencyStore(dbContext, TimeProvider.System);
        var result = store.TryGet(buyerId, "expired-key");

        Assert.Null(result);
        Assert.Equal(0, await dbContext.IdempotencyRecords.CountAsync());

        await dbContext.DisposeAsync();
    }
}