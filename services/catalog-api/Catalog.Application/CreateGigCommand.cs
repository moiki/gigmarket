namespace Catalog.Application;

public sealed record CreateGigCommand(
    string Title,
    string? Description,
    decimal Price,
    string Category,
    Guid OwnerId);