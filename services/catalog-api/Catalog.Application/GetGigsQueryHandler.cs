using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed class GetGigsQueryHandler(IGigRepository gigs)
    : IRequestHandler<GetGigsQuery, PagedResult<Gig>>
{
    public Task<PagedResult<Gig>> Handle(GetGigsQuery query, CancellationToken cancellationToken)
    {
        var status = query.Status ?? GigStatus.Active;
        return Task.FromResult(gigs.GetGigs(status, query.Category, query.MinPrice, query.MaxPrice, query.Page, query.PageSize));
    }
}