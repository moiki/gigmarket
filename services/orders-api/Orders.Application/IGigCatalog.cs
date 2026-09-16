using BuildingBlocks.Common;

namespace Orders.Application;

public sealed record GigInfo(Guid Id, decimal Price, Guid OwnerId, bool IsActive);

public interface IGigCatalog
{
    Task<Result<GigInfo>> GetByIdAsync(Guid gigId, CancellationToken cancellationToken);
}