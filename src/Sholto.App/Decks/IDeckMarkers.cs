using Sholto.App.Library.Markers;

namespace Sholto.App.Decks;

/// <summary>Memory-cue markers on the loaded tracks: drop one at a deck's playhead and keep each deck's
/// marker overlay in step with what is saved for its track. Needs the database; until
/// <see cref="Attach"/> it does nothing.</summary>
public interface IDeckMarkers
{
    /// <summary>Raised after a marker was dropped: the deck index and the position in seconds. (The same fact is
    /// published on the bus as <c>MarkerAdded</c>.)</summary>
    event Action<int, double>? MarkerAdded;

    /// <summary>The database is up: from now on a track that finishes loading gets its saved markers.</summary>
    void Attach(IMarkerService markers);

    /// <summary>Drop a marker on the given deck at its current playback position and persist it, then
    /// refresh that deck's marker overlay. Nothing happens without a database, a loaded track, or a
    /// catalog entry for it.</summary>
    Task AddAsync(int deck);
}
