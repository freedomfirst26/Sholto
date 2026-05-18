using ATL;

namespace OpenDJ.Library;

public static class TrackScanner
{
    private static readonly HashSet<string> SupportedExtensions =
        [".wav", ".mp3", ".flac", ".aiff", ".aif"];

    public static Task<IReadOnlyList<Track>> ScanAsync(
        string directory,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(directory, cancellationToken), cancellationToken);

    private static IReadOnlyList<Track> Scan(string directory, CancellationToken ct)
    {
        if (!Directory.Exists(directory))
            return [];

        return Directory
            .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .TakeWhile(_ => !ct.IsCancellationRequested)
            .Where(f => SupportedExtensions.Contains(
                Path.GetExtension(f).ToLowerInvariant()))
            .Select(ReadTrack)
            .OfType<Track>()
            .OrderBy(t => t.Artist)
            .ThenBy(t => t.Title)
            .ToList();
    }

    private static Track? ReadTrack(string path)
    {
        try
        {
            var meta = new ATL.Track(path);
            return new Track(
                FilePath: Path.GetFullPath(path),
                Title: string.IsNullOrWhiteSpace(meta.Title)
                    ? Path.GetFileNameWithoutExtension(path)
                    : meta.Title,
                Artist: string.IsNullOrWhiteSpace(meta.Artist)
                    ? "Unknown"
                    : meta.Artist,
                Duration: TimeSpan.FromMilliseconds(meta.DurationMs)
            );
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            return null;
        }
    }
}
