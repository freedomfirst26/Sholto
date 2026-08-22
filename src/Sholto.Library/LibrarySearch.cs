namespace Sholto.Library;

/// <summary>
/// Filter the music library against a free-text query. Designed to grow
/// without touching callers: today it scans <see cref="Track.Title"/> and
/// <see cref="Track.Artist"/>; when <c>Track</c> gains Genre / Tags / etc.
/// they get appended to the searchable haystack here and every consumer
/// automatically benefits.
///
/// Match semantics: whitespace-separated tokens are AND'ed; each token is a
/// case-insensitive substring match against the haystack. Empty/whitespace
/// query returns the input unfiltered. This is the same behaviour as
/// "spotlight" / VSCode / iTunes search.
/// </summary>
public static class LibrarySearch
{
    public static IEnumerable<Track> Filter(string query, IEnumerable<Track> tracks)
    {
        if (string.IsNullOrWhiteSpace(query)) return tracks;
        var tokens = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return tracks;
        return tracks.Where(t => MatchesAll(Haystack(t), tokens));
    }

    /// <summary>The string each track is matched against. Concatenates every
    /// indexed metadata field with single-space separators so cross-field
    /// queries ("silence late" → "Silence Groove Element Late") work without
    /// caller awareness. Future fields (Genre, Tags) get appended here.</summary>
    private static string Haystack(Track t) => $"{t.Artist} {t.Title}";

    private static bool MatchesAll(string haystack, string[] tokens)
    {
        foreach (var tok in tokens)
            if (haystack.IndexOf(tok, StringComparison.OrdinalIgnoreCase) < 0)
                return false;
        return true;
    }
}
