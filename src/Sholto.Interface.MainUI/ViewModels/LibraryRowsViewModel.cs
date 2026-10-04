using System.Collections.ObjectModel;
using Sholto.App.Analysis.Harmony;
using Sholto.Data;

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
    IEventHandler<HarmonyReferenceChanged>
{
    private readonly ITrackRowFactory _rowFactory;
    private readonly Dictionary<string, TrackRow> _byPath = [];
    private Key? _referenceKey;

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

    /// <summary>Re-emit theme-derived bindings on each row so KeyBrush re-evaluates against the new
    /// palette. Cheaper than a static event subscription (which would pin every row until app exit).</summary>
    public void RefreshThemeBindings()
    {
        foreach (var row in Items) row.RefreshThemeBindings();
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
                row.ReferenceKey = _referenceKey;
            }
            _byPath[summary.FilePath] = row;
            Items.Add(row);
        }
    }

    public void Handle(in TrackSummaryChanged e)
    {
        if (_byPath.TryGetValue(e.Summary.FilePath, out var row)) row.Apply(e.Summary);
    }

    public void Handle(in HarmonyReferenceChanged e)
    {
        _referenceKey = e.Key is { } k ? new Key(k.PitchClass, k.IsMajor) : null;
        foreach (var row in Items) row.ReferenceKey = _referenceKey;
    }

    private bool SameTrack(TrackRow row, TrackSummary summary) =>
        row.Title == summary.Title && row.Artist == summary.Artist && row.Duration == summary.Duration;
}
