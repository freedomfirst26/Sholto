using System.ComponentModel;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>The Glance search overlay: a fit-ranked track table beside a rail of shortlist, recent loads,
/// crates and tags. Presentation state lives here (open, query, zone, highlight); ranking, loading and the
/// shortlist are the App's, reached through the bus.</summary>
public interface IGlanceViewModel : INotifyPropertyChanged
{
    IGlanceHeaderViewModel Header { get; }

    bool IsOpen { get; }

    /// <summary>What the person typed. Kept across close and reopen.</summary>
    string Query { get; set; }

    /// <summary>The deck a load goes to, 0-based.</summary>
    int Target { get; }

    /// <summary>The ranked rows, replaced as a whole per result.</summary>
    IReadOnlyList<GlanceRow> Rows { get; }

    /// <summary>The M of "N of M": tracks in the chip scope (the whole catalogue with no chips). Follows the
    /// applied result.</summary>
    int ScopeCount { get; }

    /// <summary>The crates and tags narrowing the list, in pick order. All must match. Kept across close and reopen.</summary>
    IReadOnlyList<GlanceChip> Chips { get; }

    /// <summary>The slow indicator: a re-rank the person asked for has taken over 150 ms, and once shown it stays
    /// at least 300 ms. Whether and how to show it under reduced motion is the view's call.</summary>
    bool IsReassessing { get; }

    /// <summary>What to say when chips leave nothing, from the applied result, e.g. "No tracks in Peak time tagged
    /// Vocal" or "No “bsn” in Peak time tagged Vocal"; null while there are no chips.</summary>
    string? ScopeEmptyText { get; }

    /// <summary>Motion is wanted: the view settles the new rows in rather than swapping them.</summary>
    bool AnimateResults { get; }

    /// <summary>Raised when a result has replaced <see cref="Rows"/>: the view's cue to settle them in.</summary>
    event Action? ResultsReplaced;

    /// <summary>"BPM 124–128", "KEY 8A", "#techno": what the query was understood as.</summary>
    IReadOnlyList<string> FilterChips { get; }

    GlanceZone Zone { get; }

    int TableIndex { get; }

    /// <summary><see cref="GlanceRailHeader"/>, <see cref="GlanceRailTrack"/>, <see cref="GlanceRailCrate"/> and
    /// <see cref="GlanceRailTag"/> items.</summary>
    IReadOnlyList<object> RailItems { get; }

    int RailIndex { get; }

    /// <summary>The highlighted row's file when it is a track; null on a crate, a tag, or nothing.</summary>
    string? HighlightedPath { get; }

    /// <summary>What Enter does now: "Load to Deck 1", "Add filter", "Remove filter", or empty.</summary>
    string ActionText { get; }

    /// <summary>What Ctrl+Enter does now: "Show in library" on a rail crate or tag, otherwise empty.</summary>
    string AlternateActionText { get; }

    /// <summary>Raised on open; the view selects the whole query so typing replaces it.</summary>
    event Action? SelectAllOnOpen;

    void Open();

    void Close();

    /// <summary>Fit to the other deck.</summary>
    void FlipTarget();

    void SetTarget(int deck);

    /// <summary>Move the highlight in the current zone, skipping headers, stopping at the ends.</summary>
    void Move(int delta);

    void ToggleZone();

    /// <summary>The faint rest of a tag name after a trailing <c>#fragment</c> in the query, e.g. " and Bass"
    /// after "#drum". Null when nothing completes; empty when the fragment is the whole name.</summary>
    string? CompletionSuffix { get; }

    /// <summary>Tab while a completion shows: turns the <c>#fragment</c> into that tag's chip and keeps the
    /// other words. False when nothing completes, so the key switches zone instead.</summary>
    bool AcceptTagCompletion();

    /// <summary>Enter: load a track to <see cref="Target"/>, or toggle the chip of a rail crate or tag and stay open.</summary>
    void Activate();

    /// <summary>Enter or a click on a rail crate or tag: add its chip, or remove it when already there. Picking one
    /// clears the typed free words; <c>bpm:</c>, <c>key:</c> and <c>#</c> tokens stay. Anything else does nothing.</summary>
    void ActivateRailItem();

    /// <summary>Ctrl+Enter: filter the library by the rail crate or tag and close, leaving the chips alone.
    /// Anywhere else it is <see cref="Activate"/>.</summary>
    void ActivateAlternate();

    /// <summary>Backspace in an empty box: remove the newest chip. False when the query is not empty or there is
    /// no chip, so the key edits the text.</summary>
    bool RemoveLastChip();

    void RemoveChip(GlanceChip chip);

    /// <summary>Shift+1 / Shift+2 and LOAD: aim at <paramref name="deck"/> and load the highlighted track.</summary>
    void LoadTo(int deck);

    /// <summary>Add the highlighted track to the shortlist, or remove it.</summary>
    void ToggleShortlistOnHighlight();

    /// <summary>The Q key: toggles the shortlist only while the query box is empty. False when it did nothing,
    /// so the key types a letter.</summary>
    bool TryShortlistKey();
}
