using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sholto.Music;

namespace Sholto.App.ViewModels;

/// <summary>A non-selectable section header in the unified search list.</summary>
public sealed record SearchHeader(string Text);

/// <summary>
/// Drives the spacebar search overlay. Holds the live query string, recomputes
/// <see cref="Results"/> against the master <see cref="TrackRow"/> list as the
/// user types, and tracks the keyboard-highlighted row inside the overlay.
/// <para>The actual filter logic lives in <see cref="Sholto.Music.LibrarySearch"/>
/// so it can be unit-tested without UI dependencies.</para>
/// </summary>
public sealed class SearchViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<TrackRow> _all;
    private Sholto.Storage.TagService? _tagService;
    private Sholto.Storage.CrateService? _crateService;

    public void SetTagService(Sholto.Storage.TagService service)
    {
        _tagService = service;
        _ = RefreshTagHitsAsync();
    }

    public void SetCrateService(Sholto.Storage.CrateService service)
    {
        _crateService = service;
        _ = RefreshCrateHitsAsync();
    }

    public ObservableCollection<Sholto.Storage.TagSearchHit> TagHits { get; } = new();
    public event Action<string>? TagPicked;
    public void PickTag(string name) => TagPicked?.Invoke(name);

    /// <summary>Crates whose name matches the query — the CRATES section of the
    /// grouped results. Empty query shows all crates so space-bar is a crate browser.</summary>
    public ObservableCollection<Sholto.Storage.CrateSummary> CrateHits { get; } = new();
    public event Action<Sholto.Storage.CrateSummary>? CratePicked;
    public void PickCrate(Sholto.Storage.CrateSummary crate) => CratePicked?.Invoke(crate);

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

    /// <summary>The unified, keyboard-navigable list shown in the overlay: a CRATES
    /// header + crate rows, then a TRACKS header + track rows. Headers are skipped by
    /// <see cref="Move"/> so the highlight only ever lands on a real item.</summary>
    public ObservableCollection<object> Items { get; } = new();

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value) return;
            _selectedIndex = value;
            Notify();
            Notify(nameof(SelectedItem));
            Notify(nameof(SelectedRow));
        }
    }

    /// <summary>The highlighted item — a <see cref="TrackRow"/>, a
    /// <see cref="Sholto.Storage.CrateSummary"/>, or null.</summary>
    public object? SelectedItem =>
        SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    /// <summary>The highlighted track, if the highlight is on a track row.</summary>
    public TrackRow? SelectedRow => SelectedItem as TrackRow;

    /// <summary>Move the highlight by <paramref name="delta"/>, skipping headers.</summary>
    public void Move(int delta)
    {
        if (Items.Count == 0) return;
        int i = SelectedIndex;
        for (int step = 0; step < Items.Count; step++)
        {
            i = (i + delta + Items.Count) % Items.Count;
            if (Items[i] is not SearchHeader) { SelectedIndex = i; return; }
        }
    }

    private void BuildItems()
    {
        Items.Clear();
        if (CrateHits.Count > 0)
        {
            Items.Add(new SearchHeader("📦  CRATES"));
            foreach (var c in CrateHits) Items.Add(c);
        }
        if (Results.Count > 0)
        {
            Items.Add(new SearchHeader("🎵  TRACKS"));
            foreach (var r in Results) Items.Add(r);
        }
        // Land the highlight on the first real (non-header) row.
        _selectedIndex = -1;
        Notify(nameof(Items));
        Move(1);
    }

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
        // Match each row against artist + title + its TAGS, so a query like "techno"
        // surfaces tracks tagged techno even if the word isn't in the title/artist.
        // Linear over the library (O(thousands)); cheap enough on the UI thread.
        Results.Clear();
        foreach (var row in _all)
        {
            var haystack = row.Tags.Count == 0
                ? $"{row.Artist} {row.Title}"
                : $"{row.Artist} {row.Title} {string.Join(' ', row.Tags)}";
            if (LibrarySearch.Matches(q, haystack))
                Results.Add(row);
        }

        Notify(nameof(Results));
        BuildItems();
        _ = RefreshTagHitsAsync();
        _ = RefreshCrateHitsAsync();
    }

    private async Task RefreshCrateHitsAsync()
    {
        if (_crateService is null) return;
        try
        {
            var hits = await _crateService.SearchAsync(_query);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CrateHits.Clear();
                foreach (var h in hits) CrateHits.Add(h);
                Notify(nameof(CrateHits));
                BuildItems();
            });
        }
        catch (Exception ex) { Console.WriteLine($"[Search] crate search failed: {ex.Message}"); }
    }

    private async Task RefreshTagHitsAsync()
    {
        TagHits.Clear();
        var q = _query;
        if (_tagService is null || string.IsNullOrWhiteSpace(q))
        {
            Notify(nameof(TagHits));
            return;
        }
        try
        {
            var hits = await _tagService.SearchTagsAsync(q, 10, default);
            foreach (var h in hits) TagHits.Add(h);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Search] tag search failed: {ex.Message}");
        }
        Notify(nameof(TagHits));
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
