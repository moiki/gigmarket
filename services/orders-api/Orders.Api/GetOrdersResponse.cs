namespace Orders.Api;

public sealed record GetOrdersResponse(
    IReadOnlyList<OrderResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
