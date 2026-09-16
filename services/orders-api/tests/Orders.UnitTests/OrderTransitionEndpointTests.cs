using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Common;
using Orders.Api;
using Orders.Application;
using Orders.Domain;

namespace Orders.UnitTests;

public class OrderTransitionEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly DateTime T0 = new(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);

    private static TestApiFactory NewFactory()
    {
        var catalog = new FakeGigCatalog(_ => Task.FromResult<Result<GigInfo>>(OrderErrors.GigNotFound));
        return new TestApiFactory(catalog);
    }

    private static Order NewOrder(OrderStatus status = OrderStatus.Created)
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 25m, T0).Value;

        if (status is OrderStatus.Confirmed or OrderStatus.Cancelled)
            order.Confirm();
        if (status == OrderStatus.Cancelled)
            order.Cancel();

        return order;
    }

    [Fact]
    public async Task Confirm_WithCreatedOrder_ReturnsConfirmedAndPersists()
    {
        using var factory = NewFactory();
        var order = NewOrder();
        factory.Orders.Add(order);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/orders/{order.Id}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(OrderStatus.Confirmed, body.Status);

        var reread = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", JsonOptions);
        Assert.NotNull(reread);
        Assert.Equal(OrderStatus.Confirmed, reread.Status);
    }

    [Fact]
    public async Task Confirm_WithUnknownOrder_ReturnsNotFound()
    {
        using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/orders/{Guid.NewGuid()}/confirm", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_WithAlreadyConfirmedOrder_ReturnsConflict()
    {
        using var factory = NewFactory();
        var order = NewOrder(OrderStatus.Confirmed);
        factory.Orders.Add(order);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/orders/{order.Id}/confirm", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_WithCreatedOrder_ReturnsCancelledAndPersists()
    {
        using var factory = NewFactory();
        var order = NewOrder();
        factory.Orders.Add(order);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(OrderStatus.Cancelled, body.Status);

        var reread = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", JsonOptions);
        Assert.NotNull(reread);
        Assert.Equal(OrderStatus.Cancelled, reread.Status);
    }

    [Fact]
    public async Task Cancel_WithConfirmedOrder_ReturnsCancelled()
    {
        using var factory = NewFactory();
        var order = NewOrder(OrderStatus.Confirmed);
        factory.Orders.Add(order);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(OrderStatus.Cancelled, body.Status);
    }

    [Fact]
    public async Task Cancel_WithCancelledOrder_ReturnsConflict()
    {
        using var factory = NewFactory();
        var order = NewOrder(OrderStatus.Cancelled);
        factory.Orders.Add(order);
        using var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/orders/{order.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
