namespace Catalog.Api;

public sealed record GetGigsResponse(
    IReadOnlyList<GigResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);