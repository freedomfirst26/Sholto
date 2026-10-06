namespace Sholto.Data;

/// <summary>Filter and rank the visible library rows against the query and against the deck that is not
/// <paramref name="TargetDeck"/>. The App snapshots rows and reference on the app thread, then scores off it.</summary>
/// <param name="Query">The raw search text, including any BPM, key or tag filters.</param>
/// <param name="TargetDeck">The deck the track would be loaded into; the other deck is the reference.</param>
public readonly record struct RankTracks(string Query, int TargetDeck) : IQuery<Task<RankedTracks>>
{
    /// <summary>Crate ids the rows must all belong to (AND). A <c>default</c> struct has null; treat null as empty.</summary>
    public IReadOnlyList<int> CrateIds { get; init; } = [];

    /// <summary>Tag names the rows must all carry (AND). Exact name, ignoring case: "house" does not match
    /// "tech house", unlike the substring <c>#tag</c> text filter. A <c>default</c> struct has null; treat null as empty.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
