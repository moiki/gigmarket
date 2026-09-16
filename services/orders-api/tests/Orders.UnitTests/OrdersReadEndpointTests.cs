using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Common;
using Orders.Api;
using Orders.Application;
using Orders.Domain;

namespace Orders.UnitTests;

public class OrdersReadEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();
    private static readonly DateTime T0 = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);

    private static TestApiFactory NewFactory()
    {
        var catalog = new FakeGigCatalog(_ => Task.FromResult<Result<GigInfo>>(OrderErrors.GigNotFound));
        return new TestApiFactory(catalog);
    }

    private static Order NewOrder(DateTime createdAt, OrderStatus status = OrderStatus.Created)
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), BuyerId, ProviderId, 25m, createdAt).Value;

        if (status == OrderStatus.Confirmed)
            order.Confirm();
        else if (status == OrderStatus.Cancelled)
            order.Cancel();

        return order;
    }

    [Fact]
    public async Task GetOrderById_WithExistingOrder_ReturnsOk()
    {
        using var factory = NewFactory();
        var order = NewOrder(T0);
        factory.Orders.Add(order);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(order.Id, body.Id);
        Assert.Equal(OrderStatus.Created, body.Status);
    }

    [Fact]
    public async Task GetOrderById_WithUnknownId_ReturnsNotFound()
    {
        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrders_ReturnsPageOrderedByCreatedAtDescending()
    {
        using var factory = NewFactory();
        var oldest = NewOrder(T0);
        var newest = NewOrder(T0.AddHours(2));
        var middle = NewOrder(T0.AddHours(1));
        factory.Orders.Add(oldest);
        factory.Orders.Add(newest);
        factory.Orders.Add(middle);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetOrdersResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(new[] { newest.Id, middle.Id, oldest.Id }, body.Items.Select(o => o.Id));
        Assert.Equal(3, body.TotalCount);
        Assert.Equal(1, body.TotalPages);
    }

    [Fact]
    public async Task GetOrders_WithStatusFilter_ReturnsOnlyMatchingOrders()
    {
        using var factory = NewFactory();
        var created = NewOrder(T0);
        var confirmed = NewOrder(T0.AddHours(1), OrderStatus.Confirmed);
        factory.Orders.Add(created);
        factory.Orders.Add(confirmed);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/orders?status=Confirmed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetOrdersResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(confirmed.Id, Assert.Single(body.Items).Id);
    }

    [Fact]
    public async Task GetOrders_WithoutMatches_ReturnsEmptyPage()
    {
        using var factory = NewFactory();
        factory.Orders.Add(NewOrder(T0));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/orders?status=Cancelled");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetOrdersResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Items);
        Assert.Equal(0, body.TotalCount);
        Assert.Equal(0, body.TotalPages);
    }

    [Theory]
    [InlineData("/api/orders?status=Nope")]
    [InlineData("/api/orders?page=0")]
    [InlineData("/api/orders?pageSize=0")]
    [InlineData("/api/orders?pageSize=101")]
    public async Task GetOrders_WithInvalidQuery_ReturnsBadRequest(string url)
    {
        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
