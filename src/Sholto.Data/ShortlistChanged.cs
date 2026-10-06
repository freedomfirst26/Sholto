namespace Sholto.Data;

/// <summary>The shortlist changed. State: a late subscriber is told the current list. An immutable snapshot
/// shared by reference (do not modify).</summary>
/// <param name="Tracks">Shortlisted tracks in insertion order; catalog tracks only.</param>
public readonly record struct ShortlistChanged(IReadOnlyList<TrackSummary> Tracks) : IStateEvent
{
    public int Slot => 0;
}
