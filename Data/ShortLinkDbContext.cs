using Microsoft.EntityFrameworkCore;
using UrlShortener.Models;

namespace UrlShortener.Data;

public class ShortLinkDbContext(DbContextOptions<ShortLinkDbContext> options) : DbContext(options)
{
    public DbSet<ShortLink> Links => Set<ShortLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var link = modelBuilder.Entity<ShortLink>();

        link.ToTable("links");
        link.HasKey(x => x.Id);
        link.Property(x => x.Code).HasMaxLength(16).IsRequired();
        link.HasIndex(x => x.Code).IsUnique();
        link.Property(x => x.LongUrl).HasMaxLength(2048).IsRequired();
        link.Property(x => x.CreatedAtUtc).IsRequired();

        base.OnModelCreating(modelBuilder);
    }
}
