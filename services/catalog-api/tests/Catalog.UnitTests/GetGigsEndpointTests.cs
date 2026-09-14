using System.Net;
using System.Text.Json;
using Catalog.Application;
using Catalog.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.UnitTests;

public class GetGigsEndpointTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static readonly DateTime T0 = new(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T4 = new(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

    private static Gig NewActiveGig(string title, DateTime createdAt)
    {
        var gig = Gig.Create(Guid.NewGuid(), title, null, 25m, "Music", OwnerId, createdAt).Value;
        gig.Publish();
        return gig;
    }

    private static Gig NewDraftGig(string title, DateTime createdAt) =>
        Gig.Create(Guid.NewGuid(), title, null, 25m, "Music", OwnerId, createdAt).Value;

    [Fact]
    public async Task Get_WithoutParameters_ReturnsActiveGigsNewestFirstWithMetadata()
    {
        using var factory = new WebApplicationFactory<Program>();
        var repository = factory.Services.GetRequiredService<IGigRepository>();
        repository.Add(NewActiveGig("Guitarra", T0));
        repository.Add(NewActiveGig("Pintura", T4));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/gigs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("Pintura", root.GetProperty("items")[0].GetProperty("title").GetString());
        Assert.Equal("Guitarra", root.GetProperty("items")[1].GetProperty("title").GetString());
        Assert.Equal("Active", root.GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Get_WithoutStatus_FiltersOutDraftGigs()
    {
        using var factory = new WebApplicationFactory<Program>();
        var repository = factory.Services.GetRequiredService<IGigRepository>();
        repository.Add(NewActiveGig("Guitarra", T0));
        repository.Add(NewDraftGig("Yoga", T4));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/gigs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        Assert.Equal("Guitarra", root.GetProperty("items")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Get_WithStatusDraft_ReturnsOnlyDraftGigs()
    {
        using var factory = new WebApplicationFactory<Program>();
        var repository = factory.Services.GetRequiredService<IGigRepository>();
        repository.Add(NewActiveGig("Guitarra", T0));
        repository.Add(NewDraftGig("Yoga", T4));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/gigs?status=Draft");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
        Assert.Equal("Yoga", root.GetProperty("items")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Get_WithPageBeyondTotal_Returns200WithEmptyItems()
    {
        using var factory = new WebApplicationFactory<Program>();
        var repository = factory.Services.GetRequiredService<IGigRepository>();
        repository.Add(NewActiveGig("Guitarra", T0));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/gigs?page=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("/api/gigs?page=0")]
    [InlineData("/api/gigs?page=-5")]
    [InlineData("/api/gigs?pageSize=0")]
    [InlineData("/api/gigs?pageSize=500")]
    [InlineData("/api/gigs?status=Deleted")]
    public async Task Get_WithInvalidQueryParams_Returns400(string url)
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithEmptyCatalog_ReturnsEmptyItemsAndZeroTotals()
    {
        using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/gigs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(0, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, root.GetProperty("totalPages").GetInt32());
    }
}