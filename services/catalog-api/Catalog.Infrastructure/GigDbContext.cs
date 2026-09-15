using Catalog.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Infrastructure;

public sealed class GigDbContext(DbContextOptions<GigDbContext> options) : DbContext(options)
{
    public DbSet<Gig> Gigs => Set<Gig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var gig = modelBuilder.Entity<Gig>();
        gig.ToTable("gigs");
        gig.HasKey(g => g.Id);
        gig.Property(g => g.Title).HasMaxLength(100).IsRequired();
        gig.Property(g => g.Description).HasMaxLength(2000);
        gig.Property(g => g.Price).HasPrecision(18, 2);
        gig.Property(g => g.Category).HasConversion<string>().HasMaxLength(20);
        gig.Property(g => g.Status).HasConversion<string>().HasMaxLength(10);
        gig.Property(g => g.OwnerId);
        gig.Property(g => g.CreatedAt);
    }
}