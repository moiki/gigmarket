using BuildingBlocks.Common;
using Catalog.Application;
using Catalog.Domain;
using Catalog.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Catalog.IntegrationTests;

public class EfGigRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;
    private readonly Guid _ownerId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("gigmarket_catalog_test")
            .Build();
        await _container.StartAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private GigDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<GigDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;
        return new GigDbContext(options);
    }

    [Fact]
    public async Task Add_ThenNewDbContext_RehydratesDraftGig()
    {
        using var ctx1 = NewContext();
        await ctx1.Database.MigrateAsync();
        var gig = Gig.Create(Guid.NewGuid(), "Clases de guitarra", null, 25m, "Music", _ownerId, DateTime.UtcNow).Value;
        ctx1.Gigs.Add(gig);
        await ctx1.SaveChangesAsync();

        using var ctx2 = NewContext();
        var rehydrated = await ctx2.Gigs.FindAsync(gig.Id);

        Assert.NotNull(rehydrated);
        Assert.Equal("Clases de guitarra", rehydrated.Title);
        Assert.Equal(GigStatus.Draft, rehydrated.Status);
        Assert.Equal(25m, rehydrated.Price);
        Assert.Equal(GigCategory.Music, rehydrated.Category);
    }

    [Fact]
    public async Task Publish_ThenRehydrate_ShowsActiveInGetGigs()
    {
        using var ctx1 = NewContext();
        await ctx1.Database.MigrateAsync();
        var gig = Gig.Create(Guid.NewGuid(), "Pintura abstracta", null, 80m, "Design", _ownerId, DateTime.UtcNow).Value;
        ctx1.Gigs.Add(gig);
        await ctx1.SaveChangesAsync();

        using var ctx2 = NewContext();
        var materialized = await ctx2.Gigs.FindAsync(gig.Id);
        var publishResult = materialized!.Publish();
        Assert.True(publishResult.IsSuccess);
        await ctx2.SaveChangesAsync();

        using var ctx3 = NewContext();
        var repo = new EfGigRepository(ctx3);
        var result = repo.GetGigs(GigStatus.Active, 1, 20);

        Assert.Single(result.Items);
        Assert.Equal(gig.Id, result.Items[0].Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetGigs_FiltersAndPaginatesAgainstDatabase()
    {
        using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
        var activeIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var gig = Gig.Create(Guid.NewGuid(), $"Gig {i + 1}", null, 25m, "Music", _ownerId, DateTime.UtcNow.AddMinutes(i)).Value;
            gig.Publish();
            ctx.Gigs.Add(gig);
            activeIds.Add(gig.Id);
        }
        var draft = Gig.Create(Guid.NewGuid(), "Draft gig", null, 25m, "Music", _ownerId, DateTime.UtcNow).Value;
        ctx.Gigs.Add(draft);
        await ctx.SaveChangesAsync();

        var repo = new EfGigRepository(ctx);

        var page1 = repo.GetGigs(GigStatus.Active, 1, 2);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.True(page1.Items[0].CreatedAt >= page1.Items[1].CreatedAt);

        var page2 = repo.GetGigs(GigStatus.Active, 2, 2);
        Assert.Single(page2.Items);
        Assert.Contains(page2.Items[0].Id, activeIds);
    }

    [Fact]
    public async Task Migrations_ApplyIncrementally()
    {
        using var ctx = NewContext();
        await ctx.Database.MigrateAsync();
        var appliedMigrations = await ctx.Database.GetAppliedMigrationsAsync();

        Assert.Contains(appliedMigrations, m => m.EndsWith("InitialCreate", StringComparison.Ordinal));
    }
}