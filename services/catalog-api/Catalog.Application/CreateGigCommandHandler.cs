using BuildingBlocks.Common;
using Catalog.Domain;
using MediatR;

namespace Catalog.Application;

public sealed class CreateGigCommandHandler(IGigRepository gigs, TimeProvider clock)
    : IRequestHandler<CreateGigCommand, Result<Gig>>
{
    public Task<Result<Gig>> Handle(CreateGigCommand command, CancellationToken cancellationToken)
    {
        var result = Gig.Create(
            Guid.NewGuid(),
            command.Title,
            command.Description,
            command.Price,
            command.Category,
            command.OwnerId,
            clock.GetUtcNow().UtcDateTime);

        if (!result.IsSuccess)
            return Task.FromResult(result);

        gigs.Add(result.Value);
        return Task.FromResult(result);
    }
}