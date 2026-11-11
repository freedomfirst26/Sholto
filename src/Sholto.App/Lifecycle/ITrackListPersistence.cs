namespace Sholto.App.Lifecycle;

/// <summary>Restores and saves the Track List, migrates the retired shortlist, and loads All Tracks on a first launch.</summary>
public interface ITrackListPersistence
{
    /// <summary>Subscribes to the list's changes, then starts the restore (on the thread pool) and keeps its
    /// task; the restore runs the one-time shortlist migration after it.</summary>
    void Start();

    /// <summary>First launch only (no saved list at all): waits for the restore, then creates the "All Tracks"
    /// crate, waits for the scan to file the catalog into it, and loads it. Otherwise does nothing.</summary>
    Task LoadAllTracksOnFirstLaunchAsync();
}
