using BuildingBlocks.Common;
using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed class PublishGigCommandHandler(IGigRepository gigs)
    : IRequestHandler<PublishGigCommand, Result<Gig>>
{
    public Task<Result<Gig>> Handle(PublishGigCommand command, CancellationToken cancellationToken)
    {
        var gig = gigs.GetById(command.GigId);

        if (gig is null)
            return Task.FromResult<Result<Gig>>(GigErrors.GigNotFound);

        var result = gig.Publish();

        if (result.IsSuccess)
            gigs.Update(gig);

        return Task.FromResult(result);
    }
}