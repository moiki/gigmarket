using BuildingBlocks.Common;
using Catalog.Application;
using Catalog.Domain;

namespace Catalog.Infrastructure;

public sealed class InMemoryGigRepository : IGigRepository
{
    private readonly Dictionary<Guid, Gig> _gigs = [];

    public void Add(Gig gig) => _gigs[gig.Id] = gig;

    public void Update(Gig gig) => _gigs[gig.Id] = gig;

    public Gig? GetById(Guid id) => _gigs.GetValueOrDefault(id);

    public IReadOnlyCollection<Gig> GetAll() => _gigs.Values.ToList();

    public PagedResult<Gig> GetGigs(GigStatus status, GigCategory? category, decimal? minPrice, decimal? maxPrice, int page, int pageSize)
    {
        var matching = _gigs.Values
            .Where(g => g.Status == status)
            .Where(g => !category.HasValue || g.Category == category.Value)
            .Where(g => !minPrice.HasValue || g.Price >= minPrice.Value)
            .Where(g => !maxPrice.HasValue || g.Price <= maxPrice.Value)
            .OrderByDescending(g => g.CreatedAt)
            .ToList();

        var items = matching
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling(matching.Count / (double)pageSize);

        return new PagedResult<Gig>(items, page, pageSize, matching.Count, totalPages);
    }
}