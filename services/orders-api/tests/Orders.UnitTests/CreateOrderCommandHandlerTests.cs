using BuildingBlocks.Common;
using Orders.Application;
using Orders.Domain;
using Orders.Infrastructure;

namespace Orders.UnitTests;

public class CreateOrderCommandHandlerTests
{
    private static readonly Guid BuyerId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);

    private static CreateOrderCommandHandler NewHandler(FakeGigCatalog catalog, out InMemoryOrderRepository repository)
    {
        repository = new InMemoryOrderRepository();
        return new CreateOrderCommandHandler(catalog, repository, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task Handle_WithActiveGig_CreatesOrderWithSnapshot()
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, true)));

        var handler = NewHandler(catalog, out var repository);
        var result = await handler.Handle(new CreateOrderCommand(gigId, BuyerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var order = result.Value;
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal(gigId, order.GigId);
        Assert.Equal(BuyerId, order.BuyerId);
        Assert.Equal(ProviderId, order.ProviderId);
        Assert.Equal(25m, order.Price);
        Assert.Equal(Now, order.CreatedAt);
        Assert.Single(repository.GetAll());
    }

    [Fact]
    public async Task Handle_WithUnknownGig_ReturnsGigNotFoundError()
    {
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(OrderErrors.GigNotFound));

        var handler = NewHandler(catalog, out var repository);
        var result = await handler.Handle(new CreateOrderCommand(Guid.NewGuid(), BuyerId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.GigNotFound.Code, result.Error?.Code);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public async Task Handle_WithDraftGig_ReturnsGigNotActiveError()
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, false)));

        var handler = NewHandler(catalog, out var repository);
        var result = await handler.Handle(new CreateOrderCommand(gigId, BuyerId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.GigNotActive.Code, result.Error?.Code);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public async Task Handle_WithCatalogUnavailable_ReturnsUnavailableError()
    {
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(OrderErrors.CatalogUnavailable));

        var handler = NewHandler(catalog, out var repository);
        var result = await handler.Handle(new CreateOrderCommand(Guid.NewGuid(), BuyerId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.CatalogUnavailable.Code, result.Error?.Code);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public async Task Handle_WithEmptyBuyerId_ReturnsBuyerRequiredError()
    {
        var gigId = Guid.NewGuid();
        var catalog = new FakeGigCatalog(_ =>
            Task.FromResult<Result<GigInfo>>(new GigInfo(gigId, 25m, ProviderId, true)));

        var handler = NewHandler(catalog, out var repository);
        var result = await handler.Handle(new CreateOrderCommand(gigId, Guid.Empty), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(OrderErrors.BuyerRequired.Code, result.Error?.Code);
        Assert.Empty(repository.GetAll());
    }
}