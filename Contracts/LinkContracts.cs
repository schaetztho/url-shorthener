namespace UrlShortener.Contracts;

/// <summary>Payload for creating a short link.</summary>
public sealed record CreateLinkRequest(string Url);

/// <summary>A stored short link, including the full short URL.</summary>
public sealed record LinkResponse(
    string Code,
    string LongUrl,
    string ShortUrl,
    DateTime CreatedAtUtc,
    int Clicks);
