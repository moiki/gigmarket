using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed class GetGigByIdQueryHandler(IGigRepository gigs)
    : IRequestHandler<GetGigByIdQuery, Gig?>
{
    public Task<Gig?> Handle(GetGigByIdQuery query, CancellationToken cancellationToken)
        => Task.FromResult(gigs.GetById(query.GigId));
}