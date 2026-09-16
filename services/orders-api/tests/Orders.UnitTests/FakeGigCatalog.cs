using BuildingBlocks.Common;
using Orders.Application;

namespace Orders.UnitTests;

internal sealed class FakeGigCatalog(Func<Guid, Task<Result<GigInfo>>> handler) : IGigCatalog
{
    public Task<Result<GigInfo>> GetByIdAsync(Guid gigId, CancellationToken cancellationToken) => handler(gigId);
}