using BuildingBlocks.Common;
using Catalog.Domain;

namespace Catalog.Application;

public interface IGigRepository
{
    void Add(Gig gig);

    void Update(Gig gig);

    Gig? GetById(Guid id);

    PagedResult<Gig> GetGigs(GigStatus status, GigCategory? category, decimal? minPrice, decimal? maxPrice, int page, int pageSize);
}