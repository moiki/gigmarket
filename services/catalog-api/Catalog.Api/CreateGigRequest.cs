namespace Catalog.Api;

public sealed record CreateGigRequest(
    string Title,
    string? Description,
    decimal Price,
    string Category,
    Guid OwnerId);