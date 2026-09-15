using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Catalog.Infrastructure;

public sealed class GigDbContextDesignTimeFactory : IDesignTimeDbContextFactory<GigDbContext>
{
    public GigDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GigDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=gigmarket_catalog;Username=postgres;Password=postgres")
            .Options;

        return new GigDbContext(options);
    }
}