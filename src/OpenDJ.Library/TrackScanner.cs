using ATL;

namespace OpenDJ.Library;

public static class TrackScanner
{
    private static readonly HashSet<string> SupportedExtensions =
        [".wav", ".mp3", ".flac", ".aiff", ".aif"];

    public static IReadOnlyList<Track> Scan(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        return Directory
            .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
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
                FilePath: path,
                Title: string.IsNullOrWhiteSpace(meta.Title)
                    ? Path.GetFileNameWithoutExtension(path)
                    : meta.Title,
                Artist: string.IsNullOrWhiteSpace(meta.Artist)
                    ? "Unknown"
                    : meta.Artist,
                Duration: TimeSpan.FromMilliseconds(meta.DurationMs)
            );
        }
        catch { return null; }
    }
}
