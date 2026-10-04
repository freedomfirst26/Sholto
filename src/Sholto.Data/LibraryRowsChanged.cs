namespace Sholto.Data;

/// <summary>The visible library rows were replaced (scan, filter, filter cleared). State: a late subscriber
/// is told the current rows. The rows are an immutable snapshot shared by reference (do not modify); later
/// per-track changes arrive as <see cref="TrackSummaryChanged"/>.</summary>
/// <param name="Rows">The visible rows, in display order.</param>
/// <param name="Version">Increases with every replacement.</param>
public readonly record struct LibraryRowsChanged(IReadOnlyList<TrackSummary> Rows, int Version) : IStateEvent
{
    public int Slot => 0;
}
