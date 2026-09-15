using BuildingBlocks.Common;

namespace Catalog.Domain;

public sealed class Gig
{
    private Gig(Guid id, string title, string? description, decimal price, GigCategory category, Guid ownerId, DateTime createdAt)
    {
        Id = id;
        Title = title;
        Description = description;
        Price = price;
        Category = category;
        OwnerId = ownerId;
        Status = GigStatus.Draft;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Title { get; }

    public string? Description { get; }

    public decimal Price { get; }

    public GigCategory Category { get; }

    public Guid OwnerId { get; }

    public GigStatus Status { get; private set; }

    public DateTime CreatedAt { get; }

    public static Result<Gig> Create(Guid id, string? title, string? description, decimal price, string category, Guid ownerId, DateTime createdAt)
    {
        title = title?.Trim() ?? string.Empty;

        if (id == Guid.Empty)
            return Result<Gig>.Fail(GigErrors.InvalidId);
        if (title.Length == 0)
            return Result<Gig>.Fail(GigErrors.TitleRequired);
        if (title.Length > 100)
            return Result<Gig>.Fail(GigErrors.TitleTooLong);
        if (description?.Length > 2000)
            return Result<Gig>.Fail(GigErrors.DescriptionTooLong);
        if (price <= 0 || price > 100000)
            return Result<Gig>.Fail(GigErrors.InvalidPrice);
        if (ownerId == Guid.Empty)
            return Result<Gig>.Fail(GigErrors.OwnerRequired);
        if (!Enum.TryParse<GigCategory>(category, ignoreCase: true, out var parsedCategory) || !Enum.IsDefined(parsedCategory))
            return Result<Gig>.Fail(GigErrors.UnknownCategory);

        return Result<Gig>.Ok(new Gig(id, title, description, price, parsedCategory, ownerId, createdAt));
    }

    public Result<Gig> Publish()
    {
        if (Status != GigStatus.Draft)
            return Result<Gig>.Fail(GigErrors.NotDraftStatus);

        Status = GigStatus.Active;
        return Result<Gig>.Ok(this);
    }
}