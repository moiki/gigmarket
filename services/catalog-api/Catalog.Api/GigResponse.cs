using Catalog.Domain;

namespace Catalog.Api;

public sealed record GigResponse(
    Guid Id,
    string Title,
    string? Description,
    decimal Price,
    GigCategory Category,
    GigStatus Status,
    Guid OwnerId,
    DateTime CreatedAt)
{
    public static GigResponse From(Gig gig) =>
        new(gig.Id, gig.Title, gig.Description, gig.Price, gig.Category, gig.Status, gig.OwnerId, gig.CreatedAt);
}