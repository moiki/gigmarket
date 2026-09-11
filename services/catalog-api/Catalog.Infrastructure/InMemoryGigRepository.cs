using Catalog.Application;
using Catalog.Domain;

namespace Catalog.Infrastructure;

public sealed class InMemoryGigRepository : IGigRepository
{
    private readonly Dictionary<Guid, Gig> _gigs = [];

    public void Add(Gig gig) => _gigs[gig.Id] = gig;

    public Gig? GetById(Guid id) => _gigs.GetValueOrDefault(id);

    public IReadOnlyCollection<Gig> GetAll() => _gigs.Values.ToList();
}