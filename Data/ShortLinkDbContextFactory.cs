using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UrlShortener.Data;

/// <summary>
/// Used only by `dotnet ef` so migrations can be created without a running
/// database. The application itself gets its DbContext from dependency injection.
/// </summary>
public sealed class ShortLinkDbContextFactory : IDesignTimeDbContextFactory<ShortLinkDbContext>
{
    public ShortLinkDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=shortener;Username=shortener;Password=shortener";

        var options = new DbContextOptionsBuilder<ShortLinkDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ShortLinkDbContext(options);
    }
}
