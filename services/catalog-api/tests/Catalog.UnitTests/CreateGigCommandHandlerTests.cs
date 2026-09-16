using BuildingBlocks.Common;
using Catalog.Application;
using Catalog.Domain;
using Catalog.Infrastructure;

namespace Catalog.UnitTests;

public class CreateGigCommandHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);

    private static CreateGigCommandHandler NewHandler(out InMemoryGigRepository repository)
    {
        repository = new InMemoryGigRepository();
        return new CreateGigCommandHandler(repository, new FakeTimeProvider(Now));
    }

    private static Task<Result<Gig>> Create(CreateGigCommandHandler handler, CreateGigCommand command) =>
        handler.Handle(command, CancellationToken.None);

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessAndStoresGig()
    {
        var handler = NewHandler(out var repository);

        var result = await Create(handler, new CreateGigCommand("Clases de guitarra", "Nivel inicial", 25m, "Music", OwnerId));

        Assert.True(result.IsSuccess);
        Assert.Equal(GigStatus.Draft, result.Value.Status);
        Assert.Equal(Now, result.Value.CreatedAt);
        Assert.Single(repository.GetAll());

        var stored = repository.GetById(result.Value.Id);
        Assert.NotNull(stored);
        Assert.Equal("Clases de guitarra", stored.Title);
        Assert.Equal(25m, stored.Price);
        Assert.Equal(GigCategory.Music, stored.Category);
        Assert.Equal(OwnerId, stored.OwnerId);
    }

    [Fact]
    public async Task Handle_WithEmptyTitle_ReturnsErrorAndDoesNotStoreGig()
    {
        var handler = NewHandler(out var repository);

        var result = await Create(handler, new CreateGigCommand(string.Empty, null, 25m, "Music", OwnerId));

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.TitleRequired.Code, result.Error?.Code);
        Assert.Empty(repository.GetAll());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Handle_WithInvalidPrice_ReturnsErrorAndDoesNotStoreGig(decimal price)
    {
        var handler = NewHandler(out var repository);

        var result = await Create(handler, new CreateGigCommand("Clases de guitarra", null, price, "Music", OwnerId));

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.InvalidPrice.Code, result.Error?.Code);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public async Task Handle_WithUnknownCategory_ReturnsErrorAndDoesNotStoreGig()
    {
        var handler = NewHandler(out var repository);

        var result = await Create(handler, new CreateGigCommand("Clases de guitarra", null, 25m, "Cooking", OwnerId));

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.UnknownCategory.Code, result.Error?.Code);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public async Task Handle_WithEmptyOwnerId_ReturnsErrorAndDoesNotStoreGig()
    {
        var handler = NewHandler(out var repository);

        var result = await Create(handler, new CreateGigCommand("Clases de guitarra", null, 25m, "Music", Guid.Empty));

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.OwnerRequired.Code, result.Error?.Code);
        Assert.Empty(repository.GetAll());
    }
}