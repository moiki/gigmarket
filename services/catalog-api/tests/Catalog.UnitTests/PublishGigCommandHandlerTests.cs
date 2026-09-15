using BuildingBlocks.Common;
using Catalog.Application;
using Catalog.Domain;
using Catalog.Infrastructure;

namespace Catalog.UnitTests;

public class PublishGigCommandHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);

    private static Gig NewDraftGig() =>
        Gig.Create(Guid.NewGuid(), "Clases de guitarra", null, 25m, "Music", OwnerId, Now).Value;

    private static PublishGigCommandHandler NewHandler(out InMemoryGigRepository repository, params Gig[] gigs)
    {
        repository = new InMemoryGigRepository();
        foreach (var gig in gigs)
            repository.Add(gig);
        return new PublishGigCommandHandler(repository);
    }

    private static Task<Result<Gig>> Publish(PublishGigCommandHandler handler, Guid gigId) =>
        handler.Handle(new PublishGigCommand(gigId), CancellationToken.None);

    [Fact]
    public async Task Handle_WithDraftGig_ReturnsActiveAndPersistsStatus()
    {
        var gig = NewDraftGig();
        var handler = NewHandler(out var repository, gig);

        var result = await Publish(handler, gig.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(GigStatus.Active, result.Value.Status);
        Assert.Equal(GigStatus.Active, repository.GetById(gig.Id)!.Status);
    }

    [Fact]
    public async Task Handle_WithAlreadyActiveGig_ReturnsNotDraftStatusError()
    {
        var gig = NewDraftGig();
        gig.Publish();
        var handler = NewHandler(out var repository, gig);

        var result = await Publish(handler, gig.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.NotDraftStatus.Code, result.Error.Code);
        Assert.Equal(GigStatus.Active, repository.GetById(gig.Id)!.Status);
    }

    [Fact]
    public async Task Handle_WithUnknownId_ReturnsGigNotFoundError()
    {
        var handler = NewHandler(out _);

        var result = await Publish(handler, Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.GigNotFound.Code, result.Error.Code);
    }
}