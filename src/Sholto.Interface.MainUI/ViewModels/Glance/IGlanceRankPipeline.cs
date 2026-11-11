using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>Asks the App for rankings, decides which answer is shown and when, and keeps the state of the
/// result that is shown: its scope counts and the text for an empty scope.</summary>
public interface IGlanceRankPipeline
{
    /// <summary><see cref="IsReassessing"/> changed.</summary>
    event Action? ReassessingChanged;

    /// <summary>A result is to be shown. Raised after the applied scope state is recorded.</summary>
    event Action<RankTracks, RankedTracks>? Shown;

    bool IsReassessing { get; }

    bool ScopeChipsActive { get; }

    int ResultScopeCount { get; }

    IReadOnlyDictionary<int, int>? CrateCounts { get; }

    /// <summary>Case-insensitive by tag name.</summary>
    IReadOnlyDictionary<string, int>? TagCounts { get; }

    /// <summary>"No tracks in Peak time tagged Vocal" for the shown result, or null with no chips.</summary>
    string? ScopeEmptyText { get; }

    /// <summary>An event changed what the ranking depends on; the next <see cref="Tick"/> asks for one more.</summary>
    void MarkDirty();

    void Rank(string query, int target, IReadOnlyList<GlanceChip> chips, bool personInitiated);

    /// <summary>Run the slow indicator for this frame. True when the list is dirty and nothing is in flight.</summary>
    bool Tick(DateTime now);

    /// <summary>The overlay closed: show a held result, then clear the stale clock and the indicator.</summary>
    void Close();
}
