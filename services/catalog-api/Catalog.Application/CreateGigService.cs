using BuildingBlocks.Common;
using Catalog.Domain;

namespace Catalog.Application;

public sealed class CreateGigService(IGigRepository gigs, TimeProvider clock)
{
    public Result<Gig> Create(CreateGigCommand command)
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
            return result;

        gigs.Add(result.Value);
        return result;
    }
}