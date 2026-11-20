using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Contracts;
using UrlShortener.Data;
using UrlShortener.Models;

namespace UrlShortener.Endpoints;

public static class LinkEndpoints
{
    // No 0/O/1/l/I: easier to read out loud and to type from a screenshot.
    private const string Alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;
    private const int MaxCodeAttempts = 10;

    public static IEndpointRouteBuilder MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/links", CreateLink)
            .WithTags("Links")
            .WithName("CreateLink")
            .WithSummary("Create a short link")
            .WithDescription("Returns 201 with the stored link, including the generated short URL.")
            .Produces<LinkResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        app.MapGet("/api/links", GetLinks)
            .WithTags("Links")
            .WithName("GetLinks")
            .WithSummary("List all short links")
            .Produces<IEnumerable<LinkResponse>>();

        app.MapGet("/api/links/{code}", GetLink)
            .WithTags("Links")
            .WithName("GetLink")
            .WithSummary("Get one short link by code")
            .Produces<LinkResponse>()
            .Produces(StatusCodes.Status404NotFound);

        app.MapDelete("/api/links/{code}", DeleteLink)
            .WithTags("Links")
            .WithName("DeleteLink")
            .WithSummary("Delete a short link by code")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // The actual short link: a six-character code at the root. The length
        // constraint keeps it from swallowing /swagger or /healthz.
        app.MapGet("/{code:length(6)}", RedirectToLongUrl)
            .WithTags("Redirect")
            .WithName("Redirect")
            .WithSummary("Redirect to the original URL")
            .WithDescription("Counts the click and responds with HTTP 302.")
            .Produces(StatusCodes.Status302Found)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateLink(
        CreateLinkRequest request,
        ShortLinkDbContext db,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeUrl(request.Url, out var longUrl))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["url"] = ["Provide an absolute URL starting with http:// or https://."]
            });
        }

        var code = await GenerateUniqueCodeAsync(db, cancellationToken);

        var link = new ShortLink
        {
            Code = code,
            LongUrl = longUrl,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Links.Add(link);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/links/{link.Code}", ToResponse(link, httpRequest));
    }

    private static async Task<IResult> GetLinks(
        ShortLinkDbContext db,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var links = await db.Links
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Results.Ok(links.Select(link => ToResponse(link, httpRequest)));
    }

    private static async Task<IResult> GetLink(
        string code,
        ShortLinkDbContext db,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var link = await db.Links
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == code, cancellationToken);

        return link is null
            ? Results.NotFound()
            : Results.Ok(ToResponse(link, httpRequest));
    }

    private static async Task<IResult> DeleteLink(
        string code,
        ShortLinkDbContext db,
        CancellationToken cancellationToken)
    {
        var link = await db.Links.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (link is null)
        {
            return Results.NotFound();
        }

        db.Links.Remove(link);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> RedirectToLongUrl(
        string code,
        ShortLinkDbContext db,
        CancellationToken cancellationToken)
    {
        var link = await db.Links.SingleOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (link is null)
        {
            return Results.NotFound();
        }

        link.Clicks++;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Redirect(link.LongUrl);
    }

    private static async Task<string> GenerateUniqueCodeAsync(
        ShortLinkDbContext db,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var code = RandomNumberGenerator.GetString(Alphabet, CodeLength);
            if (!await db.Links.AnyAsync(x => x.Code == code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not generate a unique code. Please try again.");
    }

    private static bool TryNormalizeUrl(string? value, out string url)
    {
        url = string.Empty;

        if (string.IsNullOrWhiteSpace(value) ||
            !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        url = uri.ToString();
        return true;
    }

    private static LinkResponse ToResponse(ShortLink link, HttpRequest request) =>
        new(
            link.Code,
            link.LongUrl,
            $"{request.Scheme}://{request.Host}/{link.Code}",
            link.CreatedAtUtc,
            link.Clicks);
}
