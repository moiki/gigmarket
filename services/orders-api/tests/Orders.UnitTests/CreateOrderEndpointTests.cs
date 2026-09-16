using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Common;
using Orders.Api;
using Orders.Application;
using Orders.Domain;

namespace Orders.UnitTests;

public class CreateOrderEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();

    private static HttpClient NewClient(FakeGigCatalog catalog, out TestApiFactory factory)
    {
        factory = new TestApiFactory(catalog);
        return factory.CreateClient();
    }

    private static HttpRequestMessage NewOrderRequest(object payload, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(payload)
        };

        if (idempotencyKey is not null)
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        return request;
    }

    [Fact]
    public async Task PostOrder_WithActiveGig_ReturnsCreated()
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, true)));

        using var client = NewClient(catalog, out var factory);

        var response = await client.PostAsJsonAsync("/api/orders", new { gigId, buyerId = BuyerId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(order);
        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(gigId, order.GigId);
        Assert.Equal(BuyerId, order.BuyerId);
        Assert.Equal(ProviderId, order.ProviderId);
        Assert.Equal(25m, order.Price);
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Single(factory.Orders.GetAll());
    }

    [Fact]
    public async Task PostOrder_WithEmptyBuyerId_ReturnsUnprocessableEntity()
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, true)));

        using var client = NewClient(catalog, out var factory);

        var response = await client.PostAsJsonAsync("/api/orders", new { gigId, buyerId = Guid.Empty });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Empty(factory.Orders.GetAll());
    }

    [Fact]
    public async Task PostOrder_WithUnknownGig_ReturnsNotFound()
    {
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(OrderErrors.GigNotFound));

        using var client = NewClient(catalog, out var factory);

        var response = await client.PostAsJsonAsync("/api/orders", new { gigId = Guid.NewGuid(), buyerId = BuyerId });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(factory.Orders.GetAll());
    }

    [Fact]
    public async Task PostOrder_WithInactiveGig_ReturnsConflict()
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, false)));

        using var client = NewClient(catalog, out var factory);

        var response = await client.PostAsJsonAsync("/api/orders", new { gigId, buyerId = BuyerId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(factory.Orders.GetAll());
    }

    [Fact]
    public async Task PostOrder_WithCatalogDown_ReturnsServiceUnavailable()
    {
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(OrderErrors.CatalogUnavailable));

        using var client = NewClient(catalog, out var factory);

        var response = await client.PostAsJsonAsync("/api/orders", new { gigId = Guid.NewGuid(), buyerId = BuyerId });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(factory.Orders.GetAll());
    }

    [Fact]
    public async Task PostOrder_WithSameKeyAndPayload_ReplaysOriginalOrder()
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, true)));
        const string key = "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d";

        using var client = NewClient(catalog, out var factory);

        var first = await client.SendAsync(NewOrderRequest(new { gigId, buyerId = BuyerId }, key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(created);

        var replay = await client.SendAsync(NewOrderRequest(new { gigId, buyerId = BuyerId }, key));

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());
        var replayed = await replay.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(replayed);
        Assert.Equal(created.Id, replayed.Id);
        Assert.Single(factory.Orders.GetAll());
    }

    [Fact]
    public async Task PostOrder_WithSameKeyButDifferentGig_ReturnsConflict()
    {
        var gigA = Guid.NewGuid();
        var gigB = Guid.NewGuid();
        var catalog = new FakeGigCatalog(gigId =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, true)));
        const string key = "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d";

        using var client = NewClient(catalog, out var factory);

        var first = await client.SendAsync(NewOrderRequest(new { gigId = gigA, buyerId = BuyerId }, key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.SendAsync(NewOrderRequest(new { gigId = gigB, buyerId = BuyerId }, key));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Single(factory.Orders.GetAll());
    }

    [Fact]
    public async Task PostOrder_WithSameKeyButDifferentBuyer_CreatesIndependentOrders()
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, true)));
        const string key = "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d";

        using var client = NewClient(catalog, out var factory);

        var first = await client.SendAsync(NewOrderRequest(new { gigId, buyerId = BuyerId }, key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.SendAsync(NewOrderRequest(new { gigId, buyerId = Guid.NewGuid() }, key));

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(2, factory.Orders.GetAll().Count);
    }

    [Fact]
    public async Task PostOrder_WithKeyUsedOnFailedRequest_AllowsFreshRetry()
    {
        var gigId = Guid.NewGuid();
        Result<GigInfo> catalogResult = OrderErrors.GigNotFound;
        var catalog = new FakeGigCatalog(_ => Task.FromResult(catalogResult));
        const string key = "9f0b1a8c-1234-4a5b-8c9d-0e1f2a3b4c5d";

        using var client = NewClient(catalog, out var factory);

        var failed = await client.SendAsync(NewOrderRequest(new { gigId, buyerId = BuyerId }, key));
        Assert.Equal(HttpStatusCode.NotFound, failed.StatusCode);

        catalogResult = new GigInfo(gigId, 25m, ProviderId, true);
        var retry = await client.SendAsync(NewOrderRequest(new { gigId, buyerId = BuyerId }, key));

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Single(factory.Orders.GetAll());
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task PostOrder_WithInvalidKey_ReturnsBadRequest(string key)
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, true)));

        using var client = NewClient(catalog, out var factory);

        var response = await client.SendAsync(NewOrderRequest(new { gigId, buyerId = BuyerId }, key));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Orders.GetAll());
    }

    [Fact]
    public async Task PostOrder_WithoutKey_CreatesTwoOrders()
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, true)));

        using var client = NewClient(catalog, out var factory);

        var first = await client.SendAsync(NewOrderRequest(new { gigId, buyerId = BuyerId }));
        var second = await client.SendAsync(NewOrderRequest(new { gigId, buyerId = BuyerId }));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(2, factory.Orders.GetAll().Count);
    }
}