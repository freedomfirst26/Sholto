using System.Collections.ObjectModel;
using Sholto.Data;
using Sholto.Interface.MainUI.ViewModels.Glance;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>The library list as the screen shows it: a projection of the App's library events. It owns the
/// <see cref="TrackRow"/> collection the library list and the search overlay bind to, and keeps it in step
/// with the visible rows (<see cref="LibraryRowsChanged"/>: replaced on scan and filter) and the facts that
/// arrive per track (<see cref="TrackSummaryChanged"/>). Nothing is decided here.
/// <para>Events are published on the app thread, which is the UI thread, so the collection is changed
/// directly.</para></summary>
public sealed class LibraryRowsViewModel :
    IEventHandler<LibraryRowsChanged>,
    IEventHandler<TrackSummaryChanged>,
    IEventHandler<HarmonyReferenceChanged>,
    IGlanceRowSource
{
    private readonly ITrackRowFactory _rowFactory;
    private readonly Dictionary<string, TrackRow> _byPath = [];
    private readonly Dictionary<string, TrackRow> _outside = [];
    private IReadOnlyList<KeyRef> _mixableKeys = [];

    public LibraryRowsViewModel(IEventSubscriber subscriber, ITrackRowFactory rowFactory)
    {
        _rowFactory = rowFactory;
        // Replays the current picture to a subscriber that joins late.
        subscriber.Subscribe<HarmonyReferenceChanged>(this);
        subscriber.Subscribe<LibraryRowsChanged>(this);
        subscriber.Subscribe<TrackSummaryChanged>(this);
    }

    /// <summary>The visible rows, in display order.</summary>
    public ObservableCollection<TrackRow> Items { get; } = new();

    /// <summary>The visible row for <paramref name="path"/>, or null when it is not shown.</summary>
    public TrackRow? RowFor(string path) => _byPath.TryGetValue(path, out var row) ? row : null;

    /// <summary>The visible row for <paramref name="summary"/>, or a row built for it when the library does not
    /// show it. Those rows are kept per file and brought up to date from <see cref="TrackSummaryChanged"/>.</summary>
    public TrackRow RowFor(TrackSummary summary)
    {
        if (_byPath.TryGetValue(summary.FilePath, out var visible)) return visible;
        if (_outside.TryGetValue(summary.FilePath, out var cached))
        {
            cached.Apply(summary);
            return cached;
        }
        var row = _rowFactory.Create(summary);
        row.MixableKeys = _mixableKeys;
        _outside[summary.FilePath] = row;
        return row;
    }

    /// <summary>Re-emit theme-derived bindings on each row so KeyBrush re-evaluates against the new
    /// palette. Cheaper than a static event subscription (which would pin every row until app exit).</summary>
    public void RefreshThemeBindings()
    {
        foreach (var row in Items) row.RefreshThemeBindings();
        foreach (var row in _outside.Values) row.RefreshThemeBindings();
    }

    /// <summary>Replace the list with the new rows. A row for a track that is already shown (same file, same
    /// scanned metadata) is kept and brought up to date rather than rebuilt.</summary>
    public void Handle(in LibraryRowsChanged e)
    {
        var previous = new Dictionary<string, TrackRow>(_byPath);
        _byPath.Clear();
        Items.Clear();
        foreach (var summary in e.Rows)
        {
            if (previous.TryGetValue(summary.FilePath, out var row) && SameTrack(row, summary))
                row.Apply(summary);
            else
            {
                row = _rowFactory.Create(summary);
                row.MixableKeys = _mixableKeys;
            }
            _byPath[summary.FilePath] = row;
            _outside.Remove(summary.FilePath);
            Items.Add(row);
        }
    }

    public void Handle(in TrackSummaryChanged e)
    {
        if (_byPath.TryGetValue(e.Summary.FilePath, out var row)) row.Apply(e.Summary);
        else if (_outside.TryGetValue(e.Summary.FilePath, out var other)) other.Apply(e.Summary);
    }

    public void Handle(in HarmonyReferenceChanged e)
    {
        _mixableKeys = e.MixableKeys ?? [];
        foreach (var row in Items) row.MixableKeys = _mixableKeys;
        foreach (var row in _outside.Values) row.MixableKeys = _mixableKeys;
    }

    private bool SameTrack(TrackRow row, TrackSummary summary) =>
        row.Title == summary.Title && row.Artist == summary.Artist && row.Duration == summary.Duration;
}
