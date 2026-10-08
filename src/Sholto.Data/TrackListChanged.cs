namespace Sholto.Data;

/// <summary>The Track List changed. State: a late subscriber is told the current list. An immutable snapshot
/// shared by reference (do not modify).</summary>
/// <param name="Sources">The sources in the list, in load order.</param>
/// <param name="Count">Total songs in the list.</param>
public readonly record struct TrackListChanged(IReadOnlyList<TrackListSource> Sources, int Count) : IStateEvent
{
    /// <summary>The sources. A <c>default</c> struct has none, not null.</summary>
    public IReadOnlyList<TrackListSource> Sources { get => field ?? []; init; } = Sources;

    /// <summary>The song paths in the list, in list order. A <c>default</c> struct has none, not null.</summary>
    public IReadOnlyList<string> Paths { get => field ?? []; init; }

    public int Slot => 0;
}
