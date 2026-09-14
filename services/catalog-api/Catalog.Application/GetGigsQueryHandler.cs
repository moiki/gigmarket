using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed class GetGigsQueryHandler(IGigRepository gigs)
    : IRequestHandler<GetGigsQuery, PagedResult<Gig>>
{
    public Task<PagedResult<Gig>> Handle(GetGigsQuery query, CancellationToken cancellationToken)
    {
        var status = query.Status ?? GigStatus.Active;

        var matching = gigs.GetAll()
            .Where(g => g.Status == status)
            .OrderByDescending(g => g.CreatedAt)
            .ToList();

        var items = matching
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling(matching.Count / (double)query.PageSize);

        return Task.FromResult(new PagedResult<Gig>(items, query.Page, query.PageSize, matching.Count, totalPages));
    }
}