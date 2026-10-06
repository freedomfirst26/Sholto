namespace Sholto.Data;

/// <summary>The recent loads changed. State: a late subscriber is told the current list. An immutable snapshot
/// shared by reference (do not modify).</summary>
/// <param name="Tracks">Recently loaded tracks, newest first, at most 6.</param>
public readonly record struct RecentLoadsChanged(IReadOnlyList<TrackSummary> Tracks) : IStateEvent
{
    public int Slot => 0;
}
