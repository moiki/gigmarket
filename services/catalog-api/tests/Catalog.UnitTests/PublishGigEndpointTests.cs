using System.Net;
using System.Text.Json;
using Catalog.Application;
using Catalog.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.UnitTests;

public class PublishGigEndpointTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);

    private static Gig NewDraftGig() =>
        Gig.Create(Guid.NewGuid(), "Clases de guitarra", null, 25m, "Music", OwnerId, Now).Value;

    [Fact]
    public async Task Post_WithDraftGig_Returns200WithActiveGig()
    {
        using var factory = new WebApplicationFactory<Program>();
        var repository = factory.Services.GetRequiredService<IGigRepository>();
        var gig = NewDraftGig();
        repository.Add(gig);
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/gigs/{gig.Id}/publish", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(gig.Id.ToString(), doc.RootElement.GetProperty("id").GetString());
        Assert.Equal("Active", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Post_ThenGet_ShowsGigInPublicListing()
    {
        using var factory = new WebApplicationFactory<Program>();
        var repository = factory.Services.GetRequiredService<IGigRepository>();
        var gig = NewDraftGig();
        repository.Add(gig);
        var client = factory.CreateClient();

        var publish = await client.PostAsync($"/api/gigs/{gig.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        var list = await client.GetAsync("/api/gigs");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(gig.Id.ToString(), doc.RootElement.GetProperty("items")[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Post_WithAlreadyActiveGig_Returns422()
    {
        using var factory = new WebApplicationFactory<Program>();
        var repository = factory.Services.GetRequiredService<IGigRepository>();
        var gig = NewDraftGig();
        gig.Publish();
        repository.Add(gig);
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/gigs/{gig.Id}/publish", null);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithUnknownGig_Returns404()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/gigs/{Guid.NewGuid()}/publish", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithInvalidId_Returns400()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/gigs/not-a-guid/publish", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}