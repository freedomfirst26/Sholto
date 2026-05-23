using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Library;

namespace Sholto.App.ViewModels;

/// <summary>
/// Drives the spacebar search overlay. Holds the live query string, recomputes
/// <see cref="Results"/> against the master <see cref="TrackRow"/> list as the
/// user types, and tracks the keyboard-highlighted row inside the overlay.
/// <para>The actual filter logic lives in <see cref="Sholto.Library.LibrarySearch"/>
/// so it can be unit-tested without UI dependencies.</para>
/// </summary>
public sealed class SearchViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<TrackRow> _all;

    public SearchViewModel(ObservableCollection<TrackRow> allRows)
    {
        _all = allRows;
        _all.CollectionChanged += (_, _) => Recompute();
        Recompute();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _query = "";
    public string Query
    {
        get => _query;
        set
        {
            if (_query == value) return;
            _query = value;
            Notify();
            Recompute();
        }
    }

    /// <summary>Rows that match the current query. Re-bound to the overlay
    /// list every time the query (or the master list) changes.</summary>
    public ObservableCollection<TrackRow> Results { get; } = new();

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value) return;
            _selectedIndex = value;
            Notify();
            Notify(nameof(SelectedRow));
        }
    }

    /// <summary>The currently keyboard-highlighted row inside the overlay,
    /// or null if there are no results.</summary>
    public TrackRow? SelectedRow =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count ? Results[SelectedIndex] : null;

    /// <summary>Clear the query (called when the overlay closes so the next
    /// open starts fresh).</summary>
    public void Reset()
    {
        Query = "";
        SelectedIndex = 0;
    }

    private void Recompute()
    {
        var q = _query;
        // Filter operates on raw Track records; we project back to TrackRow by
        // looking each match up in the master list. Linear; library size is
        // O(thousands), filtering is fast enough on the UI thread.
        var matchedPaths = new HashSet<string>(
            LibrarySearch.Filter(q, _all.Select(r => r.Track)).Select(t => t.FilePath));

        Results.Clear();
        foreach (var row in _all)
            if (matchedPaths.Contains(row.Track.FilePath))
                Results.Add(row);

        if (_selectedIndex >= Results.Count) _selectedIndex = Math.Max(0, Results.Count - 1);
        Notify(nameof(Results));
        Notify(nameof(SelectedIndex));
        Notify(nameof(SelectedRow));
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
