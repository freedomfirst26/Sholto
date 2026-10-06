using Sholto.Data;

namespace Sholto.App.Glance;

/// <summary>The tracks inside the chip scope, and how many of them each crate and tag holds.</summary>
/// <param name="Rows">The scoped catalogue rows, in catalogue order.</param>
/// <param name="CrateCounts">Crate id to |scope ∩ crate|; null when no chips are active.</param>
/// <param name="TagCounts">Tag (case-insensitive key) to the scope tracks carrying it; null when no chips are active.</param>
public sealed record GlanceScopeResult(
    IReadOnlyList<TrackSummary> Rows,
    IReadOnlyDictionary<int, int>? CrateCounts,
    IReadOnlyDictionary<string, int>? TagCounts);
