using Catalog.Domain;

namespace Catalog.Api;

public sealed record CreateGigResponse(
    Guid Id,
    string Title,
    string? Description,
    decimal Price,
    GigCategory Category,
    GigStatus Status,
    Guid OwnerId,
    DateTime CreatedAt);