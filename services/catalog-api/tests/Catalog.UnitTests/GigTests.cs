using Catalog.Domain;

namespace Catalog.UnitTests;

public class GigTests
{
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly DateTime CreatedAt = new(2026, 9, 11, 16, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithValidInput_ReturnsGigInDraftStatus()
    {
        var result = Gig.Create(Guid.NewGuid(), "Clases de guitarra", "Nivel inicial", 25m, "Music", OwnerId, CreatedAt);

        Assert.True(result.IsSuccess);
        Assert.Equal(GigStatus.Draft, result.Value.Status);
        Assert.Equal("Clases de guitarra", result.Value.Title);
        Assert.Equal("Nivel inicial", result.Value.Description);
        Assert.Equal(25m, result.Value.Price);
        Assert.Equal(GigCategory.Music, result.Value.Category);
        Assert.Equal(OwnerId, result.Value.OwnerId);
        Assert.Equal(CreatedAt, result.Value.CreatedAt);
    }

    [Fact]
    public void Create_WithSurroundingWhitespace_TrimsTitle()
    {
        var result = Gig.Create(Guid.NewGuid(), "  Clases de guitarra  ", null, 25m, "Music", OwnerId, CreatedAt);

        Assert.True(result.IsSuccess);
        Assert.Equal("Clases de guitarra", result.Value.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyTitle_ReturnsError(string title)
    {
        var result = Gig.Create(Guid.NewGuid(), title, null, 25m, "Music", OwnerId, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.TitleRequired.Code, result.Error.Code);
    }

    [Fact]
    public void Create_WithTitleLongerThan100Chars_ReturnsError()
    {
        var result = Gig.Create(Guid.NewGuid(), new string('a', 101), null, 25m, "Music", OwnerId, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.TitleTooLong.Code, result.Error.Code);
    }

    [Fact]
    public void Create_WithDescriptionLongerThan2000Chars_ReturnsError()
    {
        var result = Gig.Create(Guid.NewGuid(), "Clases de guitarra", new string('a', 2001), 25m, "Music", OwnerId, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.DescriptionTooLong.Code, result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_WithNonPositivePrice_ReturnsError(decimal price)
    {
        var result = Gig.Create(Guid.NewGuid(), "Clases de guitarra", null, price, "Music", OwnerId, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.InvalidPrice.Code, result.Error.Code);
    }

    [Fact]
    public void Create_WithPriceAboveMax_ReturnsError()
    {
        var result = Gig.Create(Guid.NewGuid(), "Clases de guitarra", null, 100001m, "Music", OwnerId, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.InvalidPrice.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("Cooking")]
    [InlineData("")]
    public void Create_WithUnknownCategory_ReturnsError(string category)
    {
        var result = Gig.Create(Guid.NewGuid(), "Clases de guitarra", null, 25m, category, OwnerId, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.UnknownCategory.Code, result.Error.Code);
    }

    [Fact]
    public void Create_WithEmptyOwnerId_ReturnsError()
    {
        var result = Gig.Create(Guid.NewGuid(), "Clases de guitarra", null, 25m, "Music", Guid.Empty, CreatedAt);

        Assert.False(result.IsSuccess);
        Assert.Equal(GigErrors.OwnerRequired.Code, result.Error.Code);
    }
}