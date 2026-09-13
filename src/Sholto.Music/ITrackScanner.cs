namespace Sholto.Music;

/// <summary>
/// Finds playable audio files under a directory and reads their tags into
/// <see cref="Track"/> records.
///
/// A port rather than the concrete <see cref="TrackScanner"/> because scanning walks a
/// real filesystem and reads real tags: anything that merely needs "a library of
/// tracks" — a view model, a test, a harness — should be able to be handed a fixed set
/// without a directory existing.
/// </summary>
public interface ITrackScanner
{
    /// <summary>Scan <paramref name="directory"/> recursively. Returns an empty list
    /// rather than throwing when the directory is missing — the music drive is not
    /// always mounted.</summary>
    Task<IReadOnlyList<Track>> ScanAsync(string directory, CancellationToken cancellationToken = default);
}
