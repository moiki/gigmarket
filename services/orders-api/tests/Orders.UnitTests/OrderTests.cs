using Orders.Domain;

namespace Orders.UnitTests;

public class OrderTests
{
    private static readonly Guid GigId = Guid.NewGuid();
    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();
    private static readonly DateTime CreatedAt = new(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidInput_ReturnsOrderInCreatedStatus()
    {
        var result = Order.Create(Guid.NewGuid(), GigId, BuyerId, ProviderId, 25m, CreatedAt);

        Assert.True(result.IsSuccess);
        var order = result.Value;
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal(GigId, order.GigId);
        Assert.Equal(BuyerId, order.BuyerId);
        Assert.Equal(ProviderId, order.ProviderId);
        Assert.Equal(25m, order.Price);
        Assert.Equal(CreatedAt, order.CreatedAt);
    }

    [Fact]
    public void Create_WithEmptyBuyerId_ReturnsBuyerRequiredError()
    {
        var result = Order.Create(Guid.NewGuid(), GigId, Guid.Empty, ProviderId, 25m, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.BuyerRequired.Code, result.Error?.Code);
    }

    [Fact]
    public void Create_WithEmptyGigId_ReturnsGigIdRequiredError()
    {
        var result = Order.Create(Guid.NewGuid(), Guid.Empty, BuyerId, ProviderId, 25m, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.GigIdRequired.Code, result.Error?.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithNonPositivePrice_ReturnsInvalidPriceError(decimal price)
    {
        var result = Order.Create(Guid.NewGuid(), GigId, BuyerId, ProviderId, price, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.InvalidPrice.Code, result.Error?.Code);
    }

    [Fact]
    public void Create_WithEmptyProviderId_ReturnsProviderRequiredError()
    {
        var result = Order.Create(Guid.NewGuid(), GigId, BuyerId, Guid.Empty, 25m, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.ProviderRequired.Code, result.Error?.Code);
    }
}