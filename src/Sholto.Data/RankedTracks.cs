namespace Sholto.Data;

/// <summary>The answer to <see cref="RankTracks"/>: the ranked rows and what they were ranked against.</summary>
/// <param name="Rows">Matching rows, best first.</param>
/// <param name="ReferenceDeck">The deck the fit was measured against; -1 for none.</param>
/// <param name="ReferenceKey">The reference deck's key, if known.</param>
/// <param name="ReferenceBpm">The reference deck's effective BPM, if known.</param>
/// <param name="FitActive">True when fit was computed (a reference exists).</param>
/// <param name="FilterChips">Display text of the filters the query parsed, e.g. "BPM 124-128", "KEY 8A", "#techno".</param>
public sealed record RankedTracks(
    IReadOnlyList<RankedTrack> Rows,
    int ReferenceDeck,
    KeyRef? ReferenceKey,
    double? ReferenceBpm,
    bool FitActive,
    IReadOnlyList<string> FilterChips)
{
    /// <summary>Names of the crates holding the reference track, excluding "All Tracks".</summary>
    public IReadOnlyList<string> ReferenceCrates { get; init; } = [];

    /// <summary>Tracks in the chip scope (crates and tags), before the text filter.</summary>
    public int ScopeCount { get; init; }

    /// <summary>Crate id to the number of scope tracks in that crate; null when no chips are active.</summary>
    public IReadOnlyDictionary<int, int>? ScopeCrateCounts { get; init; }

    /// <summary>Tag (case-insensitive key) to the number of scope tracks carrying it; null when no chips are active.</summary>
    public IReadOnlyDictionary<string, int>? ScopeTagCounts { get; init; }
}
