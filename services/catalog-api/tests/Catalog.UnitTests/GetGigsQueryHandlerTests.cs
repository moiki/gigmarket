using BuildingBlocks.Common;
using Catalog.Application;
using Catalog.Domain;
using Catalog.Infrastructure;

namespace Catalog.UnitTests;

public class GetGigsQueryHandlerTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static readonly DateTime T0 = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T1 = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T3 = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T4 = new(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

    private static Gig NewActiveGig(string title, DateTime createdAt, decimal price = 25m, string category = "Music")
    {
        var gig = Gig.Create(Guid.NewGuid(), title, null, price, category, OwnerId, createdAt).Value;
        gig.Publish();
        return gig;
    }

    private static Gig NewDraftGig(string title, DateTime createdAt) =>
        Gig.Create(Guid.NewGuid(), title, null, 25m, "Music", OwnerId, createdAt).Value;

    private static InMemoryGigRepository RepositoryWith(params Gig[] gigs)
    {
        var repository = new InMemoryGigRepository();
        foreach (var gig in gigs)
            repository.Add(gig);
        return repository;
    }

    private static Task<PagedResult<Gig>> Query(InMemoryGigRepository repository, int page, int pageSize, GigStatus status) =>
        new GetGigsQueryHandler(repository).Handle(new GetGigsQuery(page, pageSize, status, null, null, null), CancellationToken.None);

    [Fact]
    public async Task Handle_WithActiveGigs_ReturnsOnlyActiveOrderedNewestFirst()
    {
        var older = NewActiveGig("Guitarra", T0);
        var newer = NewActiveGig("Pintura", T4);
        var repository = RepositoryWith(older, newer);

        var result = await Query(repository, 1, 20, GigStatus.Active);

        Assert.Equal(new[] { newer.Id, older.Id }, result.Items.Select(g => g.Id).ToArray());
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task Handle_WithMixedStatuses_FiltersOutDraftByActive()
    {
        var active = NewActiveGig("Guitarra", T2);
        var draft = NewDraftGig("Yoga", T4);
        var repository = RepositoryWith(active, draft);

        var result = await Query(repository, 1, 20, GigStatus.Active);

        var only = Assert.Single(result.Items);
        Assert.Equal(active.Id, only.Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WithDraftGigs_FilterByDraftReturnsOnlyDraft()
    {
        var draft = NewDraftGig("Cocina", T3);
        var active = NewActiveGig("Guitarra", T4);
        var repository = RepositoryWith(draft, active);

        var result = await Query(repository, 1, 20, GigStatus.Draft);

        var only = Assert.Single(result.Items);
        Assert.Equal(draft.Id, only.Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WithPageBeyondTotal_ReturnsEmptyItemsWithCorrectTotal()
    {
        var repository = RepositoryWith(NewActiveGig("Guitarra", T2));

        var result = await Query(repository, 999, 20, GigStatus.Active);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WithNullStatus_DefaultsToActive()
    {
        var active = NewActiveGig("Guitarra", T0);
        var draft = NewDraftGig("Yoga", T4);
        var repository = RepositoryWith(active, draft);

        var result = await new GetGigsQueryHandler(repository).Handle(new GetGigsQuery(1, 20, null, null, null, null), CancellationToken.None);

        var only = Assert.Single(result.Items);
        Assert.Equal(active.Id, only.Id);
    }

    [Fact]
    public async Task Handle_WithPagination_SplitsIntoPages()
    {
        var repository = RepositoryWith(
            NewActiveGig("A", T0),
            NewActiveGig("B", T1),
            NewActiveGig("C", T2),
            NewActiveGig("D", T3),
            NewActiveGig("E", T4));

        var page1 = await Query(repository, 1, 2, GigStatus.Active);
        var page3 = await Query(repository, 3, 2, GigStatus.Active);

        Assert.Equal(new[] { "E", "D" }, page1.Items.Select(g => g.Title).ToArray());
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal(new[] { "A" }, page3.Items.Select(g => g.Title).ToArray());
    }

    [Fact]
    public async Task Handle_WithCategoryAndPriceFilters_AppliesAllFilters()
    {
        var repository = RepositoryWith(
            NewActiveGig("Guitarra barata", T0, price: 20m, category: "Music"),
            NewActiveGig("Logo caro", T1, price: 80m, category: "Design"),
            NewActiveGig("Guitarra cara", T2, price: 80m, category: "Music"));

        var result = await new GetGigsQueryHandler(repository)
            .Handle(new GetGigsQuery(1, 20, GigStatus.Active, GigCategory.Music, 30m, 100m), CancellationToken.None);

        var only = Assert.Single(result.Items);
        Assert.Equal("Guitarra cara", only.Title);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WithEmptyCatalog_ReturnsEmptyResult()
    {
        var repository = RepositoryWith();

        var result = await Query(repository, 1, 20, GigStatus.Active);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }
}