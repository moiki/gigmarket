using Catalog.Application;
using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure;

public sealed class EfGigRepository(GigDbContext dbContext) : IGigRepository
{
    public void Add(Gig gig)
    {
        dbContext.Gigs.Add(gig);
        dbContext.SaveChanges();
    }

    public void Update(Gig gig)
    {
        dbContext.Gigs.Update(gig);
        dbContext.SaveChanges();
    }

    public Gig? GetById(Guid id) => dbContext.Gigs.Find(id);

    public PagedResult<Gig> GetGigs(GigStatus status, int page, int pageSize)
    {
        var query = dbContext.Gigs
            .Where(g => g.Status == status)
            .OrderByDescending(g => g.CreatedAt);

        var totalCount = query.Count();
        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResult<Gig>(items, page, pageSize, totalCount, totalPages);
    }
}