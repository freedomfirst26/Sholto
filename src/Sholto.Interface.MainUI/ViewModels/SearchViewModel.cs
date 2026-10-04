using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Sholto.App.Library;
using Sholto.Data;

namespace Sholto.Interface.MainUI.ViewModels;

/// <summary>
/// Drives the spacebar search overlay. Holds the live query string, recomputes
/// <see cref="Results"/> against the master <see cref="TrackRow"/> list as the
/// user types, and tracks the keyboard-highlighted row inside the overlay.
/// <para>The actual filter logic lives in <see cref="Sholto.App.Library.LibrarySearch"/>
/// so it can be unit-tested without UI dependencies.</para>
/// </summary>
public sealed class SearchViewModel : INotifyPropertyChanged, IEventHandler<LibraryDatabaseAttached>
{
    private readonly ObservableCollection<TrackRow> _all;
    private readonly ILibrarySearch _librarySearch;
    private readonly IQueryAsker _asker;
    private readonly IAppThread _appThread;
    private readonly ITagRecency _tagRecency;
    // Tag and crate hits come from the library database; until the App says it is up they stay empty.
    private bool _databaseUp;

    /// <summary>The database is up: from now on the overlay offers tag and crate hits too.</summary>
    public void Handle(in LibraryDatabaseAttached e)
    {
        _databaseUp = e.Available;
        if (!_databaseUp) return;
        _ = RefreshTagHitsAsync(_searchGen);
        _ = RefreshCrateHitsAsync(_searchGen);
    }

    public ObservableCollection<TagHit> TagHits { get; } = new();
    public event Action<string>? TagPicked;
    public void PickTag(string name)
    {
        _tagRecency.MarkUsed(name);
        TagPicked?.Invoke(name);
    }

    /// <summary>Crates whose name matches the query — the CRATES section of the
    /// grouped results. Empty query shows all crates so space-bar is a crate browser.</summary>
    public ObservableCollection<CrateRef> CrateHits { get; } = new();
    public event Action<CrateRef>? CratePicked;
    public void PickCrate(CrateRef crate) => CratePicked?.Invoke(crate);

    public SearchViewModel(
        ObservableCollection<TrackRow> allRows, ILibrarySearch librarySearch, ITagRecency tagRecency,
        IQueryAsker asker, IEventSubscriber subscriber, IAppThread appThread)
    {
        _all = allRows;
        _librarySearch = librarySearch;
        _tagRecency = tagRecency;
        _asker = asker;
        _appThread = appThread;
        _all.CollectionChanged += (_, _) => Recompute();
        Recompute();
        subscriber.Subscribe<LibraryDatabaseAttached>(this);
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
    /// <see cref="CrateRef"/>, a <see cref="TagHit"/>, or null.</summary>
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

    /// <summary>Max rows shown per section (crates / tracks / tags) so the overlay
    /// stays a quick picker, not a full listing.</summary>
    private const int PerSectionLimit = 3;

    private void BuildItems()
    {
        Items.Clear();
        if (CrateHits.Count > 0)
        {
            Items.Add(new SearchHeader("📦  CRATES"));
            foreach (var c in CrateHits.Take(PerSectionLimit)) Items.Add(c);
        }
        if (Results.Count > 0)
        {
            Items.Add(new SearchHeader("🎵  TRACKS"));
            foreach (var r in Results.Take(PerSectionLimit)) Items.Add(r);
        }
        if (TagHits.Count > 0)
        {
            Items.Add(new SearchHeader("🏷  TAGS"));
            foreach (var t in TagHits.Take(PerSectionLimit)) Items.Add(t);
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

    /// <summary>Bumped on every query change; each of the three searches captures it
    /// and only applies its results if still current — so fast typing can't paint
    /// stale hits out of order.</summary>
    private int _searchGen;

    private void Recompute()
    {
        // Fan the three collections out concurrently: tracks (in-memory, on a
        // background thread so a big library doesn't stutter the UI), tags and
        // crates (async DB queries). Each applies on the UI thread, guarded by the
        // generation token.
        int gen = ++_searchGen;
        var snapshot = _all.ToArray();   // snapshot on the UI thread for safe off-thread iteration
        _ = SearchTracksAsync(_query, gen, snapshot);
        _ = RefreshTagHitsAsync(gen);
        _ = RefreshCrateHitsAsync(gen);
    }

    private async Task SearchTracksAsync(string q, int gen, TrackRow[] snapshot)
    {
        // Match each row against artist + title + its TAGS, so "techno" surfaces
        // tracks tagged techno even when the word isn't in the title/artist.
        var matched = await Task.Run(() =>
        {
            var list = new List<TrackRow>();
            foreach (var row in snapshot)
            {
                var haystack = row.Tags.Count == 0
                    ? $"{row.Artist} {row.Title}"
                    : $"{row.Artist} {row.Title} {string.Join(' ', row.Tags)}";
                if (_librarySearch.Matches(q, haystack)) list.Add(row);
            }
            return list;
        });

        // Back on the app thread (the UI thread) before touching the bound collections.
        _appThread.Post(() =>
        {
            if (gen != _searchGen) return;   // a newer query superseded this one
            Results.Clear();
            foreach (var r in matched) Results.Add(r);
            Notify(nameof(Results));
            BuildItems();
        });
    }

    private async Task RefreshCrateHitsAsync(int gen)
    {
        if (!_databaseUp) return;
        var q = _query;
        try
        {
            var hits = await _asker.AskAsync<SearchCrates, IReadOnlyList<CrateRef>>(new SearchCrates(q)) ?? [];
            _appThread.Post(() =>
            {
                if (gen != _searchGen) return;
                CrateHits.Clear();
                foreach (var h in hits) CrateHits.Add(h);
                Notify(nameof(CrateHits));
                BuildItems();
            });
        }
        catch (Exception ex) { Console.WriteLine($"[Search] crate search failed: {ex.Message}"); }
    }

    private async Task RefreshTagHitsAsync(int gen)
    {
        if (!_databaseUp) return;
        var q = _query;
        try
        {
            const int limit = 10;
            IReadOnlyList<TagHit> hits;
            if (string.IsNullOrWhiteSpace(q))
            {
                // Empty query = "what can I search for" browse. Tags picked
                // earlier this session lead, newest first; the most-used tags
                // fill the rest, so a fresh session looks exactly as before.
                var recentNames = _tagRecency.RecentNames(limit);
                var recentHits = await _asker.AskAsync<TagsByName, IReadOnlyList<TagHit>>(new TagsByName(recentNames)) ?? [];
                var byName = recentHits.ToDictionary(h => h.Name, StringComparer.OrdinalIgnoreCase);
                var ordered = recentNames.Where(byName.ContainsKey).Select(n => byName[n]);

                var top = await _asker.AskAsync<TopTags, IReadOnlyList<TagHit>>(new TopTags(limit)) ?? [];
                hits = ordered.Concat(top)
                              .DistinctBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                              .Take(limit)
                              .ToList();
            }
            else
            {
                var matches = await _asker.AskAsync<SearchTags, IReadOnlyList<TagHit>>(new SearchTags(q, limit)) ?? [];
                hits = _tagRecency.OrderRecentFirst(matches, h => h.Name);
            }
            _appThread.Post(() =>
            {
                if (gen != _searchGen) return;
                TagHits.Clear();
                foreach (var h in hits) TagHits.Add(h);
                Notify(nameof(TagHits));
                BuildItems();   // fold the TAGS section into the visible list
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Search] tag search failed: {ex.Message}");
        }
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
