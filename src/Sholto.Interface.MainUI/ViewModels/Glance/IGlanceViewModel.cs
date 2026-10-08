using System.ComponentModel;

namespace Sholto.Interface.MainUI.ViewModels.Glance;

/// <summary>The Glance search overlay: a fit-ranked track table beside a rail of recent loads,
/// crates and tags. Presentation state lives here (open, query, zone, highlight); ranking, loading and the
/// Track List are the App's, reached through the bus.</summary>
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

    /// <summary>A shown row joined the Track List while the overlay was open; carries its file path.</summary>
    event Action<string>? AddedToTrackList;

    /// <summary>"BPM 124–128", "KEY 8A", "#techno": what the query was understood as.</summary>
    IReadOnlyList<string> FilterChips { get; }

    GlanceZone Zone { get; }

    int TableIndex { get; }

    /// <summary><see cref="GlanceRailHeader"/>, <see cref="GlanceRailCrate"/> and
    /// <see cref="GlanceRailTag"/> items.</summary>
    IReadOnlyList<object> RailItems { get; }

    int RailIndex { get; }

    /// <summary>The highlighted row's file when it is in the table; always null on the rail.</summary>
    string? HighlightedPath { get; }

    /// <summary>What Enter does now: "Load to Deck 1", "Add filter", "Remove filter", or empty.</summary>
    string ActionText { get; }

    /// <summary>Songs in the Track List.</summary>
    int TrackListCount { get; }

    /// <summary>The Deck 1 key: Ready on a song, Off on a crate or tag.</summary>
    GlanceLoadState Deck1LoadState { get; }

    /// <summary>The Deck 2 key: Ready on a song, Off on a crate or tag.</summary>
    GlanceLoadState Deck2LoadState { get; }

    /// <summary>The Track List key: Ready on a song, crate or tag, InList when it is already there, Off on nothing.</summary>
    GlanceLoadState TrackListLoadState { get; }

    /// <summary>The "+N" on the Track List key: the rail count of a crate or tag that is Ready; 0 otherwise.</summary>
    int TrackListLoadCount { get; }

    /// <summary>"Ctrl Del again: clear N" while a first Ctrl+Delete waits for its second; null otherwise.</summary>
    string? ClearArmedText { get; }

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

    /// <summary>Ctrl+L: add the highlighted song, crate or tag to the Track List and stay open. Does nothing on
    /// something already in the list.</summary>
    void LoadToTrackList();

    /// <summary>Ctrl+Delete: the first press arms ("Ctrl Del again: clear N"); a second within about 2 s empties
    /// the Track List.</summary>
    void ClearTrackList();

    /// <summary>Backspace in an empty box: remove the newest chip. False when the query is not empty or there is
    /// no chip, so the key edits the text.</summary>
    bool RemoveLastChip();

    void RemoveChip(GlanceChip chip);

    /// <summary>Shift+1 / Shift+2 and LOAD: aim at <paramref name="deck"/> and load the highlighted track.</summary>
    void LoadTo(int deck);

    /// <summary>The star: add the highlighted song to the Track List, or remove it when it is already there.</summary>
    void ToggleHighlightedInTrackList();
}
