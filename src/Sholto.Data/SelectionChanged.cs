namespace Sholto.Data;

/// <summary>The highlighted library row changed. State: a late subscriber is told the current row.</summary>
/// <param name="Index">Index into the visible rows, or -1 for none.</param>
/// <param name="TrackId">The highlighted track's catalog id, or <see cref="Guid.Empty"/> when none (or not in the catalog).</param>
public readonly record struct SelectionChanged(int Index, Guid TrackId) : IStateEvent
{
    public int Slot => 0;
}
