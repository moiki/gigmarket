using Catalog.Domain;

namespace Catalog.Application;

public interface IGigRepository
{
    void Add(Gig gig);

    Gig? GetById(Guid id);

    IReadOnlyCollection<Gig> GetAll();
}