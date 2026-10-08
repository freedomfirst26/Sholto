using Sholto.Data;

namespace Sholto.App.Library;

/// <summary>The Track List: the DJ's working set, one manual order of songs, built by loading songs, crates
/// and tags into it. Loads append, de-duplicated by file path; each song remembers the sources that brought it.
/// Every change shows the list through <see cref="ILibrarySession.ShowTrackList"/>, is announced as
/// <see cref="TrackListChanged"/> and raises <see cref="Changed"/>. App thread only.</summary>
public interface ITrackList :
    ICommandHandler<LoadSongToTrackList>,
    ICommandHandler<LoadCrateToTrackList>,
    ICommandHandler<LoadTagToTrackList>,
    ICommandHandler<RemoveFromTrackList>,
    ICommandHandler<RemoveSourceFromTrackList>,
    ICommandHandler<MoveInTrackList>,
    ICommandHandler<ClearTrackList>
{
    /// <summary>The songs in manual order, including paths the catalog does not hold now.</summary>
    IReadOnlyList<TrackListEntry> Entries { get; }

    /// <summary>The sources that still have songs in the list, in load order, with live counts.</summary>
    IReadOnlyList<TrackListSource> Sources { get; }

    /// <summary>The list changed by a command, so it should be saved. Not raised by <see cref="Restore"/>.</summary>
    event Action? Changed;

    /// <summary>Put back a saved list: the saved songs first, then anything added before the restore landed
    /// (a song in both keeps the keys of both). Shows and announces, but does not raise <see cref="Changed"/>.
    /// <paramref name="sources"/> supply the kind and name of each saved source key.</summary>
    void Restore(IReadOnlyList<TrackListEntry> entries, IReadOnlyList<TrackListSource> sources);
}
