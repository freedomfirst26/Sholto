namespace Sholto.App;

/// <summary>
/// Per-app-run session state. Tracks which songs have been loaded into a deck
/// so the library can dim/italicise them as "already played." Lives for the
/// lifetime of the process — not persisted across restarts.
///
/// Event-based: producers call <see cref="MarkPlayed"/>, listeners subscribe to
/// <see cref="TrackPlayed"/>. <see cref="MainViewModel"/> bridges deck load
/// events into MarkPlayed and routes TrackPlayed into the corresponding
/// <c>TrackRow.IsPlayed</c>.
/// </summary>
public sealed class Session
{
    private readonly HashSet<string> _played = new();
    private readonly object _gate = new();

    /// <summary>Fires once per *first* time a track is loaded into a deck. Subsequent
    /// loads of the same track don't re-fire — the row is already marked played.</summary>
    public event Action<string>? TrackPlayed;

    /// <summary>Mark <paramref name="filePath"/> as played in this session. Idempotent —
    /// re-marking a played track is a no-op (and doesn't re-fire the event).</summary>
    public void MarkPlayed(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        bool wasNew;
        lock (_gate) wasNew = _played.Add(filePath);
        if (wasNew) TrackPlayed?.Invoke(filePath);
    }

    /// <summary>Has <paramref name="filePath"/> been loaded into a deck this session?</summary>
    public bool HasPlayed(string filePath)
    {
        lock (_gate) return _played.Contains(filePath);
    }
}
