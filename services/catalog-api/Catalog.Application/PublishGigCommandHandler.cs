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
            return Task.FromResult(Result<Gig>.Fail(GigErrors.GigNotFound));

        return Task.FromResult(gig.Publish());
    }
}