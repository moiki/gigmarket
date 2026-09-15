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

    public PagedResult<Gig> GetGigs(GigStatus status, int page, int pageSize)
    {
        var matching = _gigs.Values
            .Where(g => g.Status == status)
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