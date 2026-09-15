using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed record GetGigsQuery(
    int Page,
    int PageSize,
    GigStatus? Status,
    GigCategory? Category,
    decimal? MinPrice,
    decimal? MaxPrice) : IRequest<PagedResult<Gig>>;