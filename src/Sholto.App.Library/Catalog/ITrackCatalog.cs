namespace Sholto.App.Library.Catalog;

/// <summary>Persistent catalog of the tracks the scanner has seen.</summary>
public interface ITrackCatalog
{
    /// <summary>Insert tracks whose path is unknown and refresh title, artist, duration,
    /// file size and mtime of those already known. Tracks whose file no longer exists on
    /// disk are skipped (neither inserted nor updated). Nothing is ever deleted. The
    /// result maps every catalog path to its id and lists the newly inserted ids.</summary>
    Task<TrackUpsertResult> UpsertAsync(IReadOnlyList<Track> tracks);
}
