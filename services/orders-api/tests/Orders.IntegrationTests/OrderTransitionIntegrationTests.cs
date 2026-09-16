using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api;
using Orders.Application;
using Orders.Domain;
using Orders.Infrastructure;
using Testcontainers.PostgreSql;

namespace Orders.IntegrationTests;

public class OrderTransitionIntegrationTests
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("gigmarket_orders_transition_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

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

    [Fact]
    public async Task ConfirmThenCancel_PersistsTransitions()
    {
        var (factory, gigId) = await NewFactory();
        using var api = factory;
        var client = api.CreateClient();

        var created = await client.PostAsJsonAsync("/api/orders", new { gigId, buyerId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var order = await created.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.NotNull(order);

        var confirm = await client.PostAsync($"/api/orders/{order.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var confirmed = await confirm.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.Equal(OrderStatus.Confirmed, confirmed!.Status);

        var reread = await client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}", JsonOptions);
        Assert.NotNull(reread);
        Assert.Equal(OrderStatus.Confirmed, reread.Status);

        var cancel = await client.PostAsync($"/api/orders/{order.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        var cancelled = await cancel.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.Equal(OrderStatus.Cancelled, cancelled!.Status);

        var repeat = await client.PostAsync($"/api/orders/{order.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
    }

    [Fact]
    public async Task GetOrders_WithStatusFilter_ReturnsOnlyConfirmedAfterTransition()
    {
        var (factory, gigId) = await NewFactory();
        using var api = factory;
        var client = api.CreateClient();

        var first = await client.PostAsJsonAsync("/api/orders", new { gigId, buyerId = Guid.NewGuid() });
        var second = await client.PostAsJsonAsync("/api/orders", new { gigId, buyerId = Guid.NewGuid() });
        var confirmedOrder = await first.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        var createdOrder = await second.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);

        await client.PostAsync($"/api/orders/{confirmedOrder!.Id}/confirm", null);

        var response = await client.GetAsync("/api/orders?status=Confirmed");
        var page = await response.Content.ReadFromJsonAsync<GetOrdersResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.Equal(confirmedOrder.Id, Assert.Single(page.Items).Id);
        Assert.DoesNotContain(page.Items, o => o.Id == createdOrder!.Id);
    }
}
