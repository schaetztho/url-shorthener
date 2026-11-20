namespace UrlShortener.Models;

public class ShortLink
{
    public int Id { get; set; }

    /// <summary>The public, URL-safe code, for example "aB3xY9".</summary>
    public required string Code { get; set; }

    /// <summary>The original absolute URL.</summary>
    public required string LongUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public int Clicks { get; set; }
}
