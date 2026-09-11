using Catalog.Application;
using Catalog.Domain;
using Catalog.Infrastructure;

namespace Catalog.UnitTests;

public class CreateGigServiceTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);

    private static CreateGigService NewService(out InMemoryGigRepository repository)
    {
        repository = new InMemoryGigRepository();
        return new CreateGigService(repository, new FakeTimeProvider(Now));
    }

    [Fact]
    public void Create_WithValidCommand_ReturnsSuccessAndStoresGig()
    {
        var service = NewService(out var repository);

        var result = service.Create(new CreateGigCommand("Clases de guitarra", "Nivel inicial", 25m, "Music", OwnerId));

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
    public void Create_WithEmptyTitle_ReturnsErrorAndDoesNotStoreGig()
    {
        var service = NewService(out var repository);

        var result = service.Create(new CreateGigCommand(string.Empty, null, 25m, "Music", OwnerId));

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.TitleRequired.Code, result.Error.Code);
        Assert.Empty(repository.GetAll());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WithInvalidPrice_ReturnsErrorAndDoesNotStoreGig(decimal price)
    {
        var service = NewService(out var repository);

        var result = service.Create(new CreateGigCommand("Clases de guitarra", null, price, "Music", OwnerId));

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.InvalidPrice.Code, result.Error.Code);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void Create_WithUnknownCategory_ReturnsErrorAndDoesNotStoreGig()
    {
        var service = NewService(out var repository);

        var result = service.Create(new CreateGigCommand("Clases de guitarra", null, 25m, "Cooking", OwnerId));

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.UnknownCategory.Code, result.Error.Code);
        Assert.Empty(repository.GetAll());
    }

    [Fact]
    public void Create_WithEmptyOwnerId_ReturnsErrorAndDoesNotStoreGig()
    {
        var service = NewService(out var repository);

        var result = service.Create(new CreateGigCommand("Clases de guitarra", null, 25m, "Music", Guid.Empty));

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.OwnerRequired.Code, result.Error.Code);
        Assert.Empty(repository.GetAll());
    }
}