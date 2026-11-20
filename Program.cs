using Microsoft.EntityFrameworkCore;
using UrlShortener.Data;
using UrlShortener.Endpoints;
using UrlShortener.Models;

var builder = WebApplication.CreateBuilder(args);

// Read the connection string from configuration. Inside Docker Compose this is
// provided by the ConnectionStrings__Default environment variable.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "No database configured. Set the ConnectionStrings__Default environment variable.");

builder.Services.AddDbContext<ShortLinkDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "URL Shortener API",
        Version = "v1",
        Description = "A tiny API for practising container images and Docker Compose."
    });
});

var app = builder.Build();

// Swagger is intentionally enabled in every environment so students can open
// http://localhost:8080/swagger inside the running container.
app.UseSwagger();
app.UseSwaggerUI();

app.MapLinkEndpoints();

app.MapGet("/healthz", async (ShortLinkDbContext db) =>
        await db.Database.CanConnectAsync()
            ? Results.Ok(new { status = "healthy", database = "up" })
            : Results.Json(new { status = "unhealthy", database = "down" },
                statusCode: StatusCodes.Status503ServiceUnavailable))
    .WithTags("Diagnostics")
    .WithSummary("Health check, including database connectivity")
    .ExcludeFromDescription();

await ApplyMigrationsAndSeedAsync(app);

app.Run();

static async Task ApplyMigrationsAndSeedAsync(WebApplication app)
{
    // The database container may still be starting. Retry instead of crashing
    // immediately. Compose's healthcheck makes this rare, but the retry also
    // makes a plain `docker run` (without compose) work.
    const int maxAttempts = 10;

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            app.Logger.LogWarning(ex,
                "Database not ready (attempt {Attempt}/{MaxAttempts}). Retrying in 3 seconds...",
                attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }

    if (!await db.Links.AnyAsync())
    {
        var now = DateTime.UtcNow;
        db.Links.AddRange(
            new ShortLink { Code = "dotnet", LongUrl = "https://dotnet.microsoft.com", CreatedAtUtc = now },
            new ShortLink { Code = "docker", LongUrl = "https://docs.docker.com", CreatedAtUtc = now });
        await db.SaveChangesAsync();
    }
}
